using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Linq;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class MySqlDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MySqlDataBaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.MySqlDataBases.ToListAsync();
            return View(list);
        }

        // ========== CONNECTION VALIDATION ==========

        /// <summary>
        /// Prueba la conexión a MySQL con los parámetros proporcionados.
        /// </summary>
        private async Task<(bool success, string message)> TestConnection(MySqlDataBaseViewModel model)
        {
            try
            {
                var connectionStringBuilder = new MySqlConnectionStringBuilder
                {
                    Server = model.Host,
                    Port = (uint)model.Port,
                    Database = model.DatabaseName,
                    UserID = model.Username,
                    Password = model.Password,
                    SslMode = model.SslMode ? MySqlSslMode.Required : MySqlSslMode.None,
                    ConnectionTimeout = 10 // 10 segundos de timeout
                };

                using (var connection = new MySqlConnection(connectionStringBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    // Ejecutar un query simple para verificar que la conexión funciona completamente
                    using (var command = new MySqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }

                    return (true, "Conexión exitosa a MySQL.");
                }
            }
            catch (MySqlException ex)
            {
                return (false, $"Error de MySQL: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}");
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de MySQL.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MySqlDataBaseViewModel model)
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
                bool exists = await _context.MySqlDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName);

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

                var mysql = new MySqlDataBase
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

                _context.MySqlDataBases.Add(mysql);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración de MySQL creada exitosamente. La conexión fue validada correctamente.";

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
        /// Actualiza una configuración de MySQL existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MySqlDataBaseViewModel model)
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

                var mysql = await _context.MySqlDataBases.FindAsync(model.Id.Value);
                if (mysql == null)
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
                if (string.IsNullOrWhiteSpace(model.Password) && string.IsNullOrWhiteSpace(mysql.Password))
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
                bool exists = await _context.MySqlDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName && m.Id != model.Id);
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
                    model.Password = mysql.Password;
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

                mysql.ConfigurationName = model.ConfigurationName;
                mysql.Host = model.Host;
                mysql.Port = model.Port;
                mysql.DatabaseName = model.DatabaseName;
                mysql.Username = model.Username ?? string.Empty;

                // Solo actualizar la contraseña si se proporciona una nueva
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    mysql.Password = model.Password; // TODO: Encriptar en producción
                }

                mysql.SslMode = model.SslMode;
                mysql.LastModifiedAt = DateTime.Now;

                _context.MySqlDataBases.Update(mysql);
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
        /// Elimina una configuración de MySQL.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var mysql = await _context.MySqlDataBases.FindAsync(id);
                if (mysql == null)
                {
                    TempData["Error"] = "Configuración no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.MySqlDataBases.Remove(mysql);
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
        /// Obtiene una configuración de MySQL por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var mysql = await _context.MySqlDataBases.FindAsync(id);
                if (mysql == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new MySqlDataBaseViewModel
                {
                    Id = mysql.Id,
                    ConfigurationName = mysql.ConfigurationName,
                    Host = mysql.Host,
                    Port = mysql.Port,
                    DatabaseName = mysql.DatabaseName,
                    Username = mysql.Username,
                    Password = mysql.Password, // TODO: En producción, no enviar la contraseña o enviarla parcialmente
                    SslMode = mysql.SslMode
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
        /// Crea un backup de la base de datos MySQL usando mysqldump y lo retorna como MemoryStream.
        /// </summary>
        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackup(int mysqlDatabaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                // Obtener configuración de MySQL
                var mysqlConfig = await _context.MySqlDataBases.FindAsync(mysqlDatabaseId);
                if (mysqlConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de MySQL no encontrada");
                }

                // Generar nombre de archivo de backup con timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{mysqlConfig.DatabaseName}_backup_{timestamp}.sql";

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

                // Buscar mysqldump en las rutas comunes
                string mysqldumpPath = FindMySqlDump();
                if (string.IsNullOrEmpty(mysqldumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró mysqldump. Asegúrate de que MySQL esté instalado y mysqldump esté en el PATH del sistema.");
                }

                // Construir argumentos para mysqldump
                string arguments = $"--host={mysqlConfig.Host} " +
                                 $"--port={mysqlConfig.Port} " +
                                 $"--user={mysqlConfig.Username} " +
                                 $"--password={mysqlConfig.Password} " +
                                 $"--databases {mysqlConfig.DatabaseName} " +
                                 $"--result-file=\"{tempBackupPath}\" " +
                                 "--single-transaction " +
                                 "--routines " +
                                 "--triggers";

                // Ejecutar mysqldump
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

                    // Esperar a que termine el proceso (máximo 5 minutos)
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

                return (true, memoryStream, fileName, mysqlConfig.DatabaseName, string.Empty);
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
        /// Busca mysqldump en las rutas comunes del sistema.
        /// </summary>
        private string FindMySqlDump()
        {
            // Intentar encontrar mysqldump en el PATH
            string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                try
                {
                    string mysqldumpPath = Path.Combine(path, "mysqldump.exe");
                    if (System.IO.File.Exists(mysqldumpPath))
                    {
                        return mysqldumpPath;
                    }
                }
                catch
                {
                    // Ignorar errores de path inválidos
                }
            }

            // Rutas comunes de instalación de MySQL en Windows
            string[] commonPaths = new[]
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

            foreach (string commonPath in commonPaths)
            {
                if (System.IO.File.Exists(commonPath))
                {
                    return commonPath;
                }
            }

            // En Linux/Mac, simplemente devolver "mysqldump" y confiar en el PATH
            if (!OperatingSystem.IsWindows())
            {
                return "mysqldump";
            }

            return string.Empty;
        }
    }
}
