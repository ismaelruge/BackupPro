//using BackupPro.Data;
//using BackupPro.Models;
//using Microsoft.Data.SqlClient;
//using Microsoft.EntityFrameworkCore;
//using System.Diagnostics;

//namespace BackupPro.Services
//{
//    /// <summary>
//    /// Servicio principal de orquestación de backups por origen de base de datos.
//    /// Genera archivos de respaldo y delega la subida/copia en segundo plano.
//    /// </summary>
//    public class BackupService
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly IWebHostEnvironment _env;
//        private readonly EmailService _emailService;
//        private readonly StorageService _storageService;
//        private readonly ILogger<BackupService> _logger;

//        public BackupService(ApplicationDbContext context, IWebHostEnvironment env, EmailService emailService, StorageService storageService, ILogger<BackupService> logger)
//        {
//            _context = context;
//            _env = env;
//            _emailService = emailService;
//            _storageService = storageService;
//            _logger = logger;
//        }

//        /// <summary>
//        /// Ejecuta backups manuales para todas las bases de datos registradas
//        /// </summary>
//        public async Task EjecutarBackupsAsync()
//        {
//            var databases = await _context.DatabaseSources.ToListAsync(); // Todas
//            string backupDir = Path.Combine(_env.ContentRootPath, "Backups");
//            Directory.CreateDirectory(backupDir);

//            foreach (var db in databases)
//            {
//                await ProcesarBackupAsync(db, backupDir);
//            }
//        }

//        /// <summary>
//        /// Método para la automatización: valida la periodicidad y ejecuta backups cuando corresponde
//        /// </summary>
//        public async Task EjecutarBackupsAutomatizadosAsync()
//        {
//            var companyConfig = await _context.CompanyConfigs.FirstOrDefaultAsync();
//            if (companyConfig == null) return;

//            string periodicidad = companyConfig.BackupFrequency; // Diario, Semanal, Mensual
//            TimeSpan intervalo = periodicidad switch
//            {
//                "Diario" => TimeSpan.FromHours(24),
//                "Semanal" => TimeSpan.FromDays(7),
//                "Mensual" => TimeSpan.FromDays(30),
//                _ => TimeSpan.FromHours(24)
//            };

//            var databases = await _context.DatabaseSources.ToListAsync();
//            string backupDir = Path.Combine(_env.ContentRootPath, "Backups");
//            Directory.CreateDirectory(backupDir);

//            foreach (var db in databases)
//            {
//                var ultimoBackup = await _context.BackupHistories
//                    .Where(h => h.DatabaseSourceId == db.Id && h.Status == "Exitoso")
//                    .OrderByDescending(h => h.Date)
//                    .FirstOrDefaultAsync();

//                bool debeHacerBackup = ultimoBackup == null || (DateTime.Now - ultimoBackup.Date) >= intervalo;

//                if (debeHacerBackup)
//                {
//                    await ProcesarBackupAsync(db, backupDir);
//                }
//            }
//        }

//        /// <summary>
//        /// Procesa el backup de una base de datos específica
//        /// </summary>
//        private async Task ProcesarBackupAsync(DatabaseSource db, string backupDir)
//        {
//            string status = "Exitoso";
//            string backupPath = "";

//            var history = new BackupHistory
//            {
//                DatabaseSourceId = db.Id,
//                DatabaseName = db.DatabaseName,
//                Date = DateTime.Now,
//                Status = status,
//                Message = "",
//                BackupPath = ""
//            };

//            try
//            {
//                // Generar archivo según tipo
//                if (db.Type == "SQLServer")
//                {
//                    backupPath = Path.Combine(backupDir, $"{db.DatabaseName}_{DateTime.Now:yyyyMMddHHmmss}.bak");

//                    string connectionString = db.UseWindowsAuth
//                        ? $"Server={db.Server};Database={db.DatabaseName};Integrated Security=True;Encrypt=False;"
//                        : $"Server={db.Server};Database={db.DatabaseName};User Id={db.User};Password={db.Password};Encrypt=False;";

//                    using var conn = new SqlConnection(connectionString);
//                    await conn.OpenAsync();

//                    using var cmd = new SqlCommand($"BACKUP DATABASE [{db.DatabaseName}] TO DISK='{backupPath}'", conn);
//                    await cmd.ExecuteNonQueryAsync();
//                }
//                else if (db.Type == "PostgreSQL")
//                {
//                    backupPath = Path.Combine(backupDir, $"{db.DatabaseName}_{DateTime.Now:yyyyMMddHHmmss}.sql");
//                    var args = new List<string>{"-h", db.Server, "-U", db.User, "-d", db.DatabaseName, "-f", backupPath};
//                    EjecutarProcesoConReintentos("pg_dump", args, envVar: "PGPASSWORD", envVal: db.Password);
//                }
//                else if (db.Type == "MySQL")
//                {
//                    backupPath = Path.Combine(backupDir, $"{db.DatabaseName}_{DateTime.Now:yyyyMMddHHmmss}.sql");
//                    var args = new List<string>{"-h", db.Server, "-u", db.User, $"-p{db.Password}", db.DatabaseName, $"--result-file={backupPath}"};
//                    EjecutarProcesoConReintentos("mysqldump", args);
//                }
//                else if (db.Type == "MongoDB")
//                {
//                    backupPath = Path.Combine(backupDir, $"{db.DatabaseName}_{DateTime.Now:yyyyMMddHHmmss}");
//                    var args = new List<string>{"--host", db.Server, "--db", db.DatabaseName, "--out", backupPath};
//                    if (!string.IsNullOrWhiteSpace(db.User)) { args.AddRange(new[]{"--username", db.User, "--password", db.Password}); }
//                    EjecutarProcesoConReintentos("mongodump", args);
//                }

//                history.BackupPath = backupPath;

//                // Subir al almacenamiento
//                await SubirAlmacenamientoAsync(backupPath, history);
//            }
//            catch (Exception ex)
//            {
//                history.Status = "Fallido";
//                history.Message = ex.Message;
//                _logger.LogError(ex, "Error en backup de {DatabaseName}", db.DatabaseName);

//                // Enviar correo al admin
//                var adminEmail = _context.CompanyConfigs.FirstOrDefault()?.AdminEmail;
//                if (!string.IsNullOrEmpty(adminEmail))
//                {
//                    string subject = $"[BackupPro] Error en backup de {db.DatabaseName}";
//                    string body = $"<p>El backup de la base de datos <b>{db.DatabaseName}</b> falló el {DateTime.Now}.</p><p>Error: {history.Message}</p>";
//                    await _emailService.SendEmailAsync(adminEmail, subject, body);
//                }
//            }

//            _context.BackupHistories.Add(history);
//            await _context.SaveChangesAsync();
//        }

//        /// <summary>
//        /// Ejecuta un comando externo con reintentos automáticos y logging.
//        /// </summary>
//        private void EjecutarComandoConReintentos(string comando, string? variable = null, string? valor = null, int maxReintentos = 3)
//        {
//            string exe = comando.Split(' ')[0];
//            if (!ComandoDisponible(exe))
//                throw new InvalidOperationException($"El comando externo '{exe}' no está disponible en el sistema.");

//            int intento = 0;
//            Exception? lastException = null;
//            for (; intento < maxReintentos; intento++)
//            {
//                try
//                {
//                    var process = new Process
//                    {
//                        StartInfo = new ProcessStartInfo
//                        {
//                            FileName = "cmd.exe",
//                            Arguments = $"/C {comando}",
//                            RedirectStandardOutput = true,
//                            RedirectStandardError = true,
//                            UseShellExecute = false,
//                            CreateNoWindow = true
//                        }
//                    };

//                    if (!string.IsNullOrEmpty(variable) && !string.IsNullOrEmpty(valor))
//                    {
//                        process.StartInfo.EnvironmentVariables[variable] = valor;
//                    }

//                    process.Start();
//                    string output = process.StandardOutput.ReadToEnd();
//                    string error = process.StandardError.ReadToEnd();
//                    process.WaitForExit();

//                    if (process.ExitCode == 0)
//                    {
//                        if (!string.IsNullOrWhiteSpace(output))
//                            _logger.LogInformation("Comando ejecutado correctamente: {Comando}. Salida: {Output}", comando, output);
//                        return;
//                    }
//                    else
//                    {
//                        _logger.LogWarning("Intento {Intento}: El comando '{Comando}' falló con código {ExitCode}. Error: {Error}", intento + 1, comando, process.ExitCode, error);
//                        lastException = new Exception($"Error al ejecutar comando: {error}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Intento {Intento}: Excepción al ejecutar comando '{Comando}'", intento + 1, comando);
//                    lastException = ex;
//                }
//                // Esperar antes de reintentar
//                Thread.Sleep(1000);
//            }
//            _logger.LogError(lastException, "El comando '{Comando}' falló después de {MaxReintentos} intentos", comando, maxReintentos);
//            throw new Exception($"El comando '{comando}' falló después de {maxReintentos} intentos", lastException);
//        }

//        /// <summary>
//        /// Verifica si un comando externo está disponible en el sistema.
//        /// </summary>
//        private bool ComandoDisponible(string comando)
//        {
//            var process = new Process
//            {
//                StartInfo = new ProcessStartInfo
//                {
//                    FileName = "where",
//                    Arguments = comando,
//                    RedirectStandardOutput = true,
//                    RedirectStandardError = true,
//                    UseShellExecute = false,
//                    CreateNoWindow = true
//                }
//            };
//            process.Start();
//            process.WaitForExit();
//            return process.ExitCode == 0;
//        }

//        /// <summary>
//        /// Ejecuta un proceso externo con argumentos, con reintentos y logging; permite setear una variable de entorno.
//        /// </summary>
//        private void EjecutarProcesoConReintentos(string fileName, IEnumerable<string> args, string? envVar = null, string? envVal = null, int maxReintentos = 3)
//        {
//            if (!ComandoDisponible(fileName))
//                throw new InvalidOperationException($"El comando externo '{fileName}' no está disponible en el sistema.");

//            int intento = 0;
//            Exception? lastException = null;
//            for (; intento < maxReintentos; intento++)
//            {
//                try
//                {
//                    var psi = new ProcessStartInfo
//                    {
//                        FileName = fileName,
//                        RedirectStandardOutput = true,
//                        RedirectStandardError = true,
//                        UseShellExecute = false,
//                        CreateNoWindow = true
//                    };
//                    foreach (var a in args)
//                    {
//                        psi.ArgumentList.Add(a);
//                    }
//                    if (!string.IsNullOrEmpty(envVar) && !string.IsNullOrEmpty(envVal))
//                    {
//                        psi.EnvironmentVariables[envVar] = envVal;
//                    }

//                    using var process = new Process { StartInfo = psi };
//                    process.Start();
//                    string output = process.StandardOutput.ReadToEnd();
//                    string error = process.StandardError.ReadToEnd();
//                    process.WaitForExit();
//                    if (process.ExitCode == 0)
//                    {
//                        if (!string.IsNullOrWhiteSpace(output))
//                            _logger.LogInformation("Comando {Exe} ejecutado correctamente. Salida: {Output}", fileName, output);
//                        return;
//                    }
//                    else
//                    {
//                        _logger.LogWarning("Intento {Intento}: '{Exe}' falló con código {ExitCode}. Error: {Error}", intento + 1, fileName, process.ExitCode, error);
//                        lastException = new Exception($"Error al ejecutar comando: {error}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Intento {Intento}: Excepción al ejecutar '{Exe}'", intento + 1, fileName);
//                    lastException = ex;
//                }
//                Thread.Sleep(1000);
//            }
//            _logger.LogError(lastException, "El comando '{Exe}' falló después de {MaxReintentos} intentos", fileName, maxReintentos);
//            throw new Exception($"El comando '{fileName}' falló después de {maxReintentos} intentos", lastException);
//        }

//        private async Task SubirAlmacenamientoAsync(string backupPath, BackupHistory history)
//        {
//            // Encola la subida para que se procese en background
//            BackupUploadBackgroundService.Enqueue(backupPath, history);
//            _logger.LogInformation("Backup encolado para subida en background: {BackupPath}", backupPath);
//            await Task.CompletedTask;
//        }
//    }
//}
