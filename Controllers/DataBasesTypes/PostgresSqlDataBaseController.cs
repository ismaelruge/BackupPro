using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Linq;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class PostgresSqlDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PostgresSqlDataBaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.PostgresSqlDataBases.ToListAsync();
            return View(list);
        }

        // ========== CONNECTION VALIDATION ==========

        /// <summary>
        /// Prueba la conexión a PostgreSQL con los parámetros proporcionados.
        /// </summary>
        private async Task<(bool success, string message)> TestConnection(PostgresSqlDataBaseViewModel model)
        {
            try
            {
                var connectionStringBuilder = new NpgsqlConnectionStringBuilder
                {
                    Host = model.Host,
                    Port = model.Port,
                    Database = model.DatabaseName,
                    Username = model.Username,
                    Password = model.Password,
                    SslMode = model.SslMode ? SslMode.Require : SslMode.Disable,
                    Timeout = 10 // 10 segundos de timeout
                };

                using (var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    // Ejecutar un query simple para verificar que la conexión funciona completamente
                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }

                    return (true, "Conexión exitosa a PostgreSQL.");
                }
            }
            catch (NpgsqlException ex)
            {
                return (false, $"Error de PostgreSQL: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}");
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de PostgreSQL.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostgresSqlDataBaseViewModel model)
        {
            try
            {
                // Validación personalizada: usuario y contraseña son requeridos
                if (string.IsNullOrWhiteSpace(model.Username))
                {
                    ModelState.AddModelError("Username", "El usuario es requerido");
                }
                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    ModelState.AddModelError("Password", "La contraseña es requerida");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    var errorMessage = "Errores de validación: " + string.Join(", ", errors);

                    // Si es una petición AJAX, devolver JSON
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si ya existe una configuración con el mismo nombre
                bool exists = await _context.PostgresSqlDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName);

                if (exists)
                {
                    var errorMessage = "Ya existe una configuración con este nombre.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Validar la conexión antes de guardar
                var (connectionSuccess, connectionMessage) = await TestConnection(model);
                if (!connectionSuccess)
                {
                    var errorMessage = $"Error al validar la conexión: {connectionMessage}";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                var postgres = new PostgresSqlDataBase
                {
                    ConfigurationName = model.ConfigurationName,
                    Host = model.Host,
                    Port = model.Port,
                    DatabaseName = model.DatabaseName,
                    Username = model.Username ?? string.Empty,
                    Password = model.Password ?? string.Empty, // TODO: Encriptar en producción
                    SslMode = model.SslMode,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.PostgresSqlDataBases.Add(postgres);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración de PostgreSQL creada exitosamente. La conexión fue validada correctamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error al crear configuración: {ex.Message}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Error"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Actualiza una configuración de PostgreSQL existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PostgresSqlDataBaseViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    var errorMessage = "ID de configuración no especificado.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                var postgres = await _context.PostgresSqlDataBases.FindAsync(model.Id.Value);
                if (postgres == null)
                {
                    var errorMessage = "Configuración no encontrada.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Validación personalizada: usuario es requerido
                if (string.IsNullOrWhiteSpace(model.Username))
                {
                    ModelState.AddModelError("Username", "El usuario es requerido");
                }

                // Si no hay contraseña nueva y no hay contraseña guardada, es un error
                if (string.IsNullOrWhiteSpace(model.Password) && string.IsNullOrWhiteSpace(postgres.Password))
                {
                    ModelState.AddModelError("Password", "La contraseña es requerida");
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    var errorMessage = "Errores de validación: " + string.Join(", ", errors);

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.PostgresSqlDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName && p.Id != model.Id);
                if (exists)
                {
                    var errorMessage = "Ya existe otra configuración con este nombre.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Si no se proporciona contraseña, mantener la actual
                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    model.Password = postgres.Password;
                }

                // Validar la conexión antes de actualizar
                var (connectionSuccess, connectionMessage) = await TestConnection(model);
                if (!connectionSuccess)
                {
                    var errorMessage = $"Error al validar la conexión: {connectionMessage}";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                postgres.ConfigurationName = model.ConfigurationName;
                postgres.Host = model.Host;
                postgres.Port = model.Port;
                postgres.DatabaseName = model.DatabaseName;
                postgres.Username = model.Username ?? string.Empty;

                // Solo actualizar la contraseña si se proporciona una nueva
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    postgres.Password = model.Password; // TODO: Encriptar en producción
                }

                postgres.SslMode = model.SslMode;
                postgres.LastModifiedAt = DateTime.Now;

                _context.PostgresSqlDataBases.Update(postgres);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración actualizada exitosamente. La conexión fue validada correctamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error al actualizar configuración: {ex.Message}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Error"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Elimina una configuración de PostgreSQL.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var postgres = await _context.PostgresSqlDataBases.FindAsync(id);
                if (postgres == null)
                {
                    TempData["Error"] = "Configuración no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.PostgresSqlDataBases.Remove(postgres);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Configuración eliminada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error al eliminar configuración: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene una configuración de PostgreSQL por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var postgres = await _context.PostgresSqlDataBases.FindAsync(id);
                if (postgres == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new PostgresSqlDataBaseViewModel
                {
                    Id = postgres.Id,
                    ConfigurationName = postgres.ConfigurationName,
                    Host = postgres.Host,
                    Port = postgres.Port,
                    DatabaseName = postgres.DatabaseName,
                    Username = postgres.Username,
                    Password = postgres.Password, // TODO: En producción, no enviar la contraseña o enviarla parcialmente
                    SslMode = postgres.SslMode
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        // ========== BACKUP OPERATIONS ==========

        /// <summary>
        /// Crea un backup de la base de datos PostgreSQL usando pg_dump y lo retorna como MemoryStream.
        /// </summary>
        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackup(int postgresDatabaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                // Obtener configuración de PostgreSQL
                var postgresConfig = await _context.PostgresSqlDataBases.FindAsync(postgresDatabaseId);
                if (postgresConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de PostgreSQL no encontrada");
                }

                // Generar nombre de archivo de backup con timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{postgresConfig.DatabaseName}_backup_{timestamp}.sql";

                // Buscar el disco con más espacio disponible
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .OrderByDescending(d => d.AvailableFreeSpace)
                    .ToList();

                if (drives.Count == 0)
                {
                    return (false, null, string.Empty, string.Empty, "No se encontraron discos disponibles para crear el backup temporal");
                }

                // Usar el disco con más espacio disponible
                string tempFolder = Path.Combine(drives[0].Name, "Temp");

                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                tempBackupPath = Path.Combine(tempFolder, fileName);

                // Buscar pg_dump en las rutas comunes
                string pgDumpPath = FindPgDump();
                if (string.IsNullOrEmpty(pgDumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró pg_dump. Asegúrate de que PostgreSQL esté instalado y pg_dump esté en el PATH del sistema.");
                }

                // Configurar variable de entorno para la contraseña (pg_dump la lee de PGPASSWORD)
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Añadir la contraseña como variable de entorno
                processStartInfo.EnvironmentVariables["PGPASSWORD"] = postgresConfig.Password;

                // Construir argumentos para pg_dump
                string arguments = $"--host={postgresConfig.Host} " +
                                 $"--port={postgresConfig.Port} " +
                                 $"--username={postgresConfig.Username} " +
                                 $"--dbname={postgresConfig.DatabaseName} " +
                                 $"--file=\"{tempBackupPath}\" " +
                                 "--format=plain " +
                                 "--no-owner " +
                                 "--no-acl " +
                                 "--clean " +
                                 "--if-exists";

                processStartInfo.Arguments = arguments;

                // Ejecutar pg_dump
                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso pg_dump");
                    }

                    // Esperar a que termine el proceso (máximo 5 minutos)
                    bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                    if (!exited)
                    {
                        process.Kill();
                        return (false, null, string.Empty, string.Empty, "El proceso pg_dump excedió el tiempo límite de 5 minutos");
                    }

                    string errorOutput = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        return (false, null, string.Empty, string.Empty, $"Error al ejecutar pg_dump: {errorOutput}");
                    }
                }

                // Verificar que el archivo se creó correctamente
                if (!System.IO.File.Exists(tempBackupPath))
                {
                    return (false, null, string.Empty, string.Empty, "El archivo de backup no se creó correctamente");
                }

                // Leer el archivo a MemoryStream
                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempBackupPath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                // Posicionar el stream al inicio
                memoryStream.Position = 0;

                // Eliminar archivo temporal
                try
                {
                    System.IO.File.Delete(tempBackupPath);
                }
                catch
                {
                    // Ignorar errores al eliminar archivo temporal
                }

                return (true, memoryStream, fileName, postgresConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                // Limpiar archivo temporal si existe
                if (!string.IsNullOrEmpty(tempBackupPath) && System.IO.File.Exists(tempBackupPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempBackupPath);
                    }
                    catch { /* Ignorar errores al eliminar */ }
                }

                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Busca pg_dump en las rutas comunes del sistema.
        /// </summary>
        private string FindPgDump()
        {
            // Intentar encontrar pg_dump en el PATH
            string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                try
                {
                    string pgDumpPath = Path.Combine(path, "pg_dump.exe");
                    if (System.IO.File.Exists(pgDumpPath))
                    {
                        return pgDumpPath;
                    }
                }
                catch
                {
                    // Ignorar errores de path inválidos
                }
            }

            // Rutas comunes de instalación de PostgreSQL en Windows
            string[] commonPaths = new[]
            {
                @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\13\bin\pg_dump.exe",
                @"C:\Program Files\PostgreSQL\12\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\18\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\17\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\16\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\15\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\14\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\13\bin\pg_dump.exe",
                @"C:\Program Files (x86)\PostgreSQL\12\bin\pg_dump.exe"
            };

            foreach (string commonPath in commonPaths)
            {
                if (System.IO.File.Exists(commonPath))
                {
                    return commonPath;
                }
            }

            // En Linux/Mac, simplemente devolver "pg_dump" y confiar en el PATH
            if (!OperatingSystem.IsWindows())
            {
                return "pg_dump";
            }

            return string.Empty;
        }
    }
}
