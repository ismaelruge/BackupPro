using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System.Linq;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class MongoDBDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<MongoDBDataBaseController> _logger;

        public MongoDBDataBaseController(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<MongoDBDataBaseController> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.MongoDBDataBases.ToListAsync();
            return View(list);
        }

        // ========== CONNECTION VALIDATION ==========

        /// <summary>
        /// Prueba la conexión a MongoDB con los parámetros proporcionados.
        /// </summary>
        private async Task<(bool success, string message)> TestConnection(MongoDBDataBaseViewModel model)
        {
            try
            {
                var connectionStringBuilder = new List<string>();
                connectionStringBuilder.Add($"mongodb://{model.Host}:{model.Port}");

                // Si tiene usuario y contraseña, construir con autenticación
                MongoClientSettings settings;
                if (!string.IsNullOrWhiteSpace(model.Username) && !string.IsNullOrWhiteSpace(model.Password))
                {
                    var credential = MongoCredential.CreateCredential(
                        "admin",  // Authentication database
                        model.Username,
                        model.Password
                    );

                    settings = new MongoClientSettings
                    {
                        Credential = credential,
                        Server = new MongoServerAddress(model.Host, model.Port),
                        ServerSelectionTimeout = TimeSpan.FromSeconds(10),
                        ConnectTimeout = TimeSpan.FromSeconds(10)
                    };

                    if (model.SslEnabled)
                    {
                        settings.UseTls = true;
                    }
                }
                else
                {
                    // Conexión sin autenticación
                    settings = new MongoClientSettings
                    {
                        Server = new MongoServerAddress(model.Host, model.Port),
                        ServerSelectionTimeout = TimeSpan.FromSeconds(10),
                        ConnectTimeout = TimeSpan.FromSeconds(10)
                    };

                    if (model.SslEnabled)
                    {
                        settings.UseTls = true;
                    }
                }

                var client = new MongoClient(settings);
                var database = client.GetDatabase(model.DatabaseName);

                // Ejecutar un comando simple para verificar la conexión
                await database.RunCommandAsync((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");

                return (true, "Conexión exitosa a MongoDB.");
            }
            catch (MongoAuthenticationException ex)
            {
                return (false, $"Error de autenticación: {ex.Message}");
            }
            catch (MongoConnectionException ex)
            {
                return (false, $"Error de conexión a MongoDB: {ex.Message}");
            }
            catch (TimeoutException)
            {
                return (false, "Tiempo de espera agotado al conectar a MongoDB. Verifica el host, puerto y configuración de red.");
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}");
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de MongoDB.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MongoDBDataBaseViewModel model)
        {
            try
            {
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
                bool exists = await _context.MongoDBDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName);

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

                var mongodb = new MongoDBDataBase
                {
                    ConfigurationName = model.ConfigurationName,
                    Host = model.Host,
                    Port = model.Port,
                    DatabaseName = model.DatabaseName,
                    Username = model.Username,
                    Password = _credentialProtector.Protect(model.Password),
                    SslEnabled = model.SslEnabled,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.MongoDBDataBases.Add(mongodb);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración de MongoDB creada exitosamente. La conexión fue validada correctamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de MongoDB");
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
        /// Actualiza una configuración de MongoDB existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(MongoDBDataBaseViewModel model)
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

                var mongodb = await _context.MongoDBDataBases.FindAsync(model.Id.Value);
                if (mongodb == null)
                {
                    var errorMessage = "Configuración no encontrada.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
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
                bool exists = await _context.MongoDBDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName && m.Id != model.Id);
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

                // Si no se proporciona contraseña, mantener la actual (descifrada solo para probar la conexión)
                bool passwordProvided = !string.IsNullOrWhiteSpace(model.Password);
                if (!passwordProvided)
                {
                    model.Password = _credentialProtector.Unprotect(mongodb.Password);
                }

                // Si no se proporciona usuario, mantener el actual
                if (string.IsNullOrWhiteSpace(model.Username))
                {
                    model.Username = mongodb.Username;
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

                mongodb.ConfigurationName = model.ConfigurationName;
                mongodb.Host = model.Host;
                mongodb.Port = model.Port;
                mongodb.DatabaseName = model.DatabaseName;
                mongodb.Username = model.Username;

                // Solo actualizar la contraseña si se proporciona una nueva
                if (passwordProvided)
                {
                    mongodb.Password = _credentialProtector.Protect(model.Password);
                }

                mongodb.SslEnabled = model.SslEnabled;
                mongodb.LastModifiedAt = DateTime.Now;

                _context.MongoDBDataBases.Update(mongodb);
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
                _logger.LogError(ex, "Error al actualizar configuración de MongoDB");
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
        /// Elimina una configuración de MongoDB.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var mongodb = await _context.MongoDBDataBases.FindAsync(id);
                if (mongodb == null)
                {
                    TempData["Error"] = "Configuración no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.MongoDBDataBases.Remove(mongodb);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Configuración eliminada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de MongoDB");
                TempData["Error"] = $"Error al eliminar configuración: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene una configuración de MongoDB por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var mongodb = await _context.MongoDBDataBases.FindAsync(id);
                if (mongodb == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new MongoDBDataBaseViewModel
                {
                    Id = mongodb.Id,
                    ConfigurationName = mongodb.ConfigurationName,
                    Host = mongodb.Host,
                    Port = mongodb.Port,
                    DatabaseName = mongodb.DatabaseName,
                    Username = mongodb.Username,
                    Password = string.Empty, // La contraseña nunca se envía al cliente; dejar en blanco para no cambiarla
                    SslEnabled = mongodb.SslEnabled,
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
        /// Crea un backup de la base de datos MongoDB usando mongodump y lo retorna como MemoryStream.
        /// </summary>
        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackup(int mongodbDatabaseId)
        {
            string tempBackupPath = string.Empty;
            string tempArchivePath = string.Empty;

            try
            {
                // Obtener configuración de MongoDB
                var mongodbConfig = await _context.MongoDBDataBases.FindAsync(mongodbDatabaseId);
                if (mongodbConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de MongoDB no encontrada");
                }

                // Generar nombre de archivo de backup con timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFolderName = $"{mongodbConfig.DatabaseName}_backup_{timestamp}";
                string fileName = $"{backupFolderName}.zip";

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

                tempBackupPath = Path.Combine(tempFolder, backupFolderName);
                tempArchivePath = Path.Combine(tempFolder, fileName);

                // Buscar mongodump en las rutas comunes
                string mongoDumpPath = FindMongoDump();
                if (string.IsNullOrEmpty(mongoDumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró mongodump. Asegúrate de que MongoDB Database Tools esté instalado y mongodump esté en el PATH del sistema.");
                }

                string? plainPassword = _credentialProtector.Unprotect(mongodbConfig.Password);
                bool hasCredentials = !string.IsNullOrWhiteSpace(mongodbConfig.Username) && !string.IsNullOrWhiteSpace(plainPassword);

                // Construir argumentos para mongodump
                var arguments = new System.Text.StringBuilder();

                // Las credenciales, si existen, se pasan mediante un archivo de configuración temporal
                // (--config) en vez de --username/--password en la línea de comandos, para que no
                // queden expuestas en la lista de procesos del sistema (ps/Task Manager).
                string? credentialsFilePath = null;
                if (hasCredentials)
                {
                    // El archivo de configuración de mongodump usa claves planas (no anidadas) que
                    // corresponden a los nombres largos de los flags de línea de comandos.
                    credentialsFilePath = Path.Combine(Path.GetTempPath(), $"mongodump_{Guid.NewGuid():N}.yaml");
                    string yaml = $"username: \"{mongodbConfig.Username}\"\n" +
                                  $"password: \"{plainPassword}\"\n" +
                                  "authenticationDatabase: \"admin\"\n";
                    await System.IO.File.WriteAllTextAsync(credentialsFilePath, yaml);

                    if (!OperatingSystem.IsWindows())
                    {
                        System.IO.File.SetUnixFileMode(credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }

                    arguments.Append($"--config=\"{credentialsFilePath}\" ");
                }

                arguments.Append($"--host={mongodbConfig.Host} ");
                arguments.Append($"--port={mongodbConfig.Port} ");
                arguments.Append($"--db={mongodbConfig.DatabaseName} ");
                arguments.Append($"--out=\"{tempBackupPath}\" ");

                // Agregar SSL si está habilitado
                if (mongodbConfig.SslEnabled)
                {
                    arguments.Append("--ssl ");
                }

                try
                {
                    // Ejecutar mongodump
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mongoDumpPath,
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso mongodump");
                        }

                        // Esperar a que termine el proceso (máximo 5 minutos)
                        bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                        if (!exited)
                        {
                            process.Kill();
                            return (false, null, string.Empty, string.Empty, "El proceso mongodump excedió el tiempo límite de 5 minutos");
                        }

                        string errorOutput = await process.StandardError.ReadToEndAsync();

                        if (process.ExitCode != 0)
                        {
                            return (false, null, string.Empty, string.Empty, $"Error al ejecutar mongodump: {errorOutput}");
                        }
                    }
                }
                finally
                {
                    if (credentialsFilePath != null)
                    {
                        try
                        {
                            System.IO.File.Delete(credentialsFilePath);
                        }
                        catch { /* Ignorar errores al eliminar el archivo de credenciales temporal */ }
                    }
                }

                // Verificar que el directorio se creó correctamente
                if (!Directory.Exists(tempBackupPath))
                {
                    return (false, null, string.Empty, string.Empty, "El directorio de backup no se creó correctamente");
                }

                // Comprimir el directorio de backup en un archivo ZIP
                System.IO.Compression.ZipFile.CreateFromDirectory(tempBackupPath, tempArchivePath);

                // Verificar que el archivo ZIP se creó
                if (!System.IO.File.Exists(tempArchivePath))
                {
                    return (false, null, string.Empty, string.Empty, "El archivo ZIP del backup no se creó correctamente");
                }

                // Leer el archivo ZIP a MemoryStream
                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempArchivePath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                // Posicionar el stream al inicio
                memoryStream.Position = 0;

                // Eliminar archivos y directorios temporales
                try
                {
                    if (Directory.Exists(tempBackupPath))
                    {
                        Directory.Delete(tempBackupPath, true);
                    }
                    if (System.IO.File.Exists(tempArchivePath))
                    {
                        System.IO.File.Delete(tempArchivePath);
                    }
                }
                catch
                {
                    // Ignorar errores al eliminar archivos temporales
                }

                return (true, memoryStream, fileName, mongodbConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                // Limpiar archivos temporales si existen
                try
                {
                    if (!string.IsNullOrEmpty(tempBackupPath) && Directory.Exists(tempBackupPath))
                    {
                        Directory.Delete(tempBackupPath, true);
                    }
                    if (!string.IsNullOrEmpty(tempArchivePath) && System.IO.File.Exists(tempArchivePath))
                    {
                        System.IO.File.Delete(tempArchivePath);
                    }
                }
                catch { /* Ignorar errores al eliminar */ }

                _logger.LogError(ex, "Error al crear backup de MongoDB {DatabaseId}", mongodbDatabaseId);
                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Busca mongodump en las rutas comunes del sistema.
        /// </summary>
        private string FindMongoDump()
        {
            // Intentar encontrar mongodump en el PATH
            string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                try
                {
                    string mongoDumpPath = Path.Combine(path, "mongodump.exe");
                    if (System.IO.File.Exists(mongoDumpPath))
                    {
                        return mongoDumpPath;
                    }
                }
                catch
                {
                    // Ignorar errores de path inválidos
                }
            }

            // Rutas comunes de instalación de MongoDB Database Tools en Windows
            string[] commonPaths = new[]
            {
                @"C:\Program Files\MongoDB\Server\8.2\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\8.1\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\8.0\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Tools\100\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\7.0\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\6.0\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\5.0\bin\mongodump.exe",
                @"C:\Program Files\MongoDB\Server\4.4\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\8.2\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\8.1\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\8.0\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Tools\100\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\7.0\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\6.0\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\5.0\bin\mongodump.exe",
                @"C:\Program Files (x86)\MongoDB\Server\4.4\bin\mongodump.exe"
            };

            foreach (string commonPath in commonPaths)
            {
                if (System.IO.File.Exists(commonPath))
                {
                    return commonPath;
                }
            }

            // En Linux/Mac, simplemente devolver "mongodump" y confiar en el PATH
            if (!OperatingSystem.IsWindows())
            {
                return "mongodump";
            }

            return string.Empty;
        }
    }
}
