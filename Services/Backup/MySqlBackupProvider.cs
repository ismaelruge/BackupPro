using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Genera un backup de una base de datos MySQL configurada usando mysqldump.</summary>
    public class MySqlBackupProvider : IDatabaseBackupProvider
    {
        private static readonly string[] WindowsCommonPaths =
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 9.0\bin\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\xampp\mysql\bin\mysqldump.exe",
            @"C:\wamp\bin\mysql\mysql8.0.27\bin\mysqldump.exe",
            @"C:\wamp64\bin\mysql\mysql8.0.27\bin\mysqldump.exe"
        };

        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<MySqlBackupProvider> _logger;

        public string DatabaseType => "MySQL";

        public MySqlBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<MySqlBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                var mysqlConfig = await _context.MySqlDataBases.FindAsync(databaseId);
                if (mysqlConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de MySQL no encontrada");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{mysqlConfig.DatabaseName}_backup_{timestamp}.sql";

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .OrderByDescending(d => d.AvailableFreeSpace)
                    .ToList();

                if (drives.Count == 0)
                {
                    return (false, null, string.Empty, string.Empty, "No se encontraron discos disponibles para crear el backup temporal");
                }

                string tempFolder = Path.Combine(drives[0].Name, "Temp");

                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                tempBackupPath = Path.Combine(tempFolder, fileName);

                string mysqldumpPath = ExecutableLocator.Find("mysqldump", WindowsCommonPaths, "mysqldump");
                if (string.IsNullOrEmpty(mysqldumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró mysqldump. Asegúrate de que MySQL esté instalado y mysqldump esté en el PATH del sistema.");
                }

                // La contraseña se pasa a mysqldump mediante un archivo de opciones temporal
                // (--defaults-extra-file) en vez de --password en la línea de comandos, para que no
                // quede expuesta en la lista de procesos del sistema (ps/Task Manager).
                string plainPassword = _credentialProtector.Unprotect(mysqlConfig.Password) ?? string.Empty;
                string credentialsFilePath = Path.Combine(Path.GetTempPath(), $"mysqldump_{Guid.NewGuid():N}.cnf");
                await File.WriteAllTextAsync(credentialsFilePath,
                    $"[client]\nuser={mysqlConfig.Username}\npassword={plainPassword}\n");

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                try
                {
                    // --defaults-extra-file debe ser el primer argumento para que mysqldump lo reconozca
                    string arguments = $"--defaults-extra-file=\"{credentialsFilePath}\" " +
                                     $"--host={mysqlConfig.Host} " +
                                     $"--port={mysqlConfig.Port} " +
                                     $"--databases {mysqlConfig.DatabaseName} " +
                                     $"--result-file=\"{tempBackupPath}\" " +
                                     "--single-transaction " +
                                     "--routines " +
                                     "--triggers";

                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mysqldumpPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso mysqldump");
                        }

                        bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                        if (!exited)
                        {
                            process.Kill();
                            return (false, null, string.Empty, string.Empty, "El proceso mysqldump excedió el tiempo límite de 5 minutos");
                        }

                        string errorOutput = await process.StandardError.ReadToEndAsync();

                        if (process.ExitCode != 0)
                        {
                            return (false, null, string.Empty, string.Empty, $"Error al ejecutar mysqldump: {errorOutput}");
                        }
                    }
                }
                finally
                {
                    try
                    {
                        File.Delete(credentialsFilePath);
                    }
                    catch { /* Ignorar errores al eliminar el archivo de credenciales temporal */ }
                }

                if (!File.Exists(tempBackupPath))
                {
                    return (false, null, string.Empty, string.Empty, "El archivo de backup no se creó correctamente");
                }

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempBackupPath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                memoryStream.Position = 0;
                TryDeleteFile(tempBackupPath);

                return (true, memoryStream, fileName, mysqlConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempBackupPath);
                _logger.LogError(ex, "Error al crear backup de MySQL {DatabaseId}", databaseId);
                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* Ignorar errores al eliminar archivo temporal */ }
        }
    }
}
