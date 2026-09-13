using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class SqlServerDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<SqlServerDataBaseController> _logger;

        public SqlServerDataBaseController(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<SqlServerDataBaseController> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.SqlServerDataBases.ToListAsync();
            return View(list);
        }

        // ========== CONNECTION VALIDATION ==========

        /// <summary>
        /// Prueba la conexión a SQL Server con los parámetros proporcionados.
        /// </summary>
        private async Task<(bool success, string message)> TestConnection(SqlServerDataBaseViewModel model)
        {
            try
            {
                var connectionStringBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = $"{model.Host},{model.Port}",
                    InitialCatalog = model.DatabaseName,
                    TrustServerCertificate = model.TrustServerCertificate,
                    ConnectTimeout = 10 // 10 segundos de timeout
                };

                if (model.IntegratedSecurity)
                {
                    connectionStringBuilder.IntegratedSecurity = true;
                }
                else
                {
                    connectionStringBuilder.UserID = model.Username ?? string.Empty;
                    connectionStringBuilder.Password = model.Password ?? string.Empty;
                }

                using (var connection = new SqlConnection(connectionStringBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    // Ejecutar un query simple para verificar que la conexión funciona completamente
                    using (var command = new SqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }

                    return (true, "Conexión exitosa a SQL Server.");
                }
            }
            catch (SqlException ex)
            {
                return (false, $"Error de SQL Server: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}");
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de SQL Server.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SqlServerDataBaseViewModel model)
        {
            try
            {
                // Validación personalizada: si no usa Integrated Security, usuario y contraseña son requeridos
                if (!model.IntegratedSecurity)
                {
                    if (string.IsNullOrWhiteSpace(model.Username))
                    {
                        ModelState.AddModelError("Username", "El usuario es requerido cuando no se usa Seguridad Integrada de Windows");
                    }
                    if (string.IsNullOrWhiteSpace(model.Password))
                    {
                        ModelState.AddModelError("Password", "La contraseña es requerida cuando no se usa Seguridad Integrada de Windows");
                    }
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
                bool exists = await _context.SqlServerDataBases.AnyAsync(s => s.ConfigurationName == model.ConfigurationName);

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

                var sqlServer = new SqlServerDataBase
                {
                    ConfigurationName = model.ConfigurationName,
                    Host = model.Host,
                    Port = model.Port,
                    DatabaseName = model.DatabaseName,
                    Username = model.Username ?? string.Empty,
                    Password = _credentialProtector.Protect(model.Password) ?? string.Empty,
                    IntegratedSecurity = model.IntegratedSecurity,
                    TrustServerCertificate = model.TrustServerCertificate,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.SqlServerDataBases.Add(sqlServer);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración de SQL Server creada exitosamente. La conexión fue validada correctamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de SQL Server");
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
        /// Actualiza una configuración de SQL Server existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SqlServerDataBaseViewModel model)
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

                var sqlServer = await _context.SqlServerDataBases.FindAsync(model.Id.Value);
                if (sqlServer == null)
                {
                    var errorMessage = "Configuración no encontrada.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Validación personalizada para Edit: si no usa Integrated Security, usuario y contraseña son requeridos
                if (!model.IntegratedSecurity)
                {
                    if (string.IsNullOrWhiteSpace(model.Username))
                    {
                        ModelState.AddModelError("Username", "El usuario es requerido cuando no se usa Seguridad Integrada de Windows");
                    }

                    // Si no hay contraseña nueva y no hay contraseña guardada, es un error
                    if (string.IsNullOrWhiteSpace(model.Password) && string.IsNullOrWhiteSpace(sqlServer.Password))
                    {
                        ModelState.AddModelError("Password", "La contraseña es requerida cuando no se usa Seguridad Integrada de Windows");
                    }
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
                bool exists = await _context.SqlServerDataBases.AnyAsync(s => s.ConfigurationName == model.ConfigurationName && s.Id != model.Id);
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
                    model.Password = _credentialProtector.Unprotect(sqlServer.Password);
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

                sqlServer.ConfigurationName = model.ConfigurationName;
                sqlServer.Host = model.Host;
                sqlServer.Port = model.Port;
                sqlServer.DatabaseName = model.DatabaseName;
                sqlServer.Username = model.Username ?? string.Empty;

                // Solo actualizar la contraseña si se proporciona una nueva
                if (passwordProvided)
                {
                    sqlServer.Password = _credentialProtector.Protect(model.Password) ?? string.Empty;
                }

                sqlServer.IntegratedSecurity = model.IntegratedSecurity;
                sqlServer.TrustServerCertificate = model.TrustServerCertificate;
                sqlServer.LastModifiedAt = DateTime.Now;

                _context.SqlServerDataBases.Update(sqlServer);
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
                _logger.LogError(ex, "Error al actualizar configuración de SQL Server");
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
        /// Elimina una configuración de SQL Server.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var sqlServer = await _context.SqlServerDataBases.FindAsync(id);
                if (sqlServer == null)
                {
                    TempData["Error"] = "Configuración no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.SqlServerDataBases.Remove(sqlServer);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Configuración eliminada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de SQL Server");
                TempData["Error"] = $"Error al eliminar configuración: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene una configuración de SQL Server por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var sqlServer = await _context.SqlServerDataBases.FindAsync(id);
                if (sqlServer == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new SqlServerDataBaseViewModel
                {
                    Id = sqlServer.Id,
                    ConfigurationName = sqlServer.ConfigurationName,
                    Host = sqlServer.Host,
                    Port = sqlServer.Port,
                    DatabaseName = sqlServer.DatabaseName,
                    Username = sqlServer.Username,
                    Password = string.Empty, // La contraseña nunca se envía al cliente; dejar en blanco para no cambiarla
                    IntegratedSecurity = sqlServer.IntegratedSecurity,
                    TrustServerCertificate = sqlServer.TrustServerCertificate
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
        /// Otorga permiso de escritura en la carpeta temporal de backups únicamente a las cuentas de
        /// servicio de SQL Server que realmente necesitan escribir el archivo .bak (el motor de SQL
        /// Server, no el proceso de esta aplicación, es quien escribe ese archivo). No se otorgan
        /// permisos a "Everyone" ni a "BUILTIN\Users": ampliar el acceso a cualquier usuario local de
        /// la máquina a una carpeta que contiene backups completos de bases de datos es un riesgo
        /// innecesario.
        /// </summary>
        private bool EnsureFolderWritePermissions(string folderPath)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(folderPath);
                var directorySecurity = directoryInfo.GetAccessControl();

                // Cuentas de servicio de SQL Server que pueden necesitar escribir en esta carpeta
                string[] accounts = new[]
                {
                    @"NT Service\MSSQLSERVER",
                    @"NT Service\SQLEXPRESS",
                    @"NT AUTHORITY\NETWORK SERVICE",
                    @"NT AUTHORITY\SYSTEM"
                };

                foreach (var account in accounts)
                {
                    try
                    {
                        var fileSystemRule = new FileSystemAccessRule(
                            account,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow);

                        directorySecurity.AddAccessRule(fileSystemRule);
                    }
                    catch
                    {
                        // Ignorar si la cuenta no existe
                        continue;
                    }
                }

                // Deshabilitar herencia y copiar permisos heredados
                directorySecurity.SetAccessRuleProtection(false, true);

                directoryInfo.SetAccessControl(directorySecurity);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Crea un backup de SQL Server y lo retorna como MemoryStream
        /// </summary>
        /// <param name="sqlServerDatabaseId">ID de la configuración de SQL Server</param>
        /// <returns>Tuple con el MemoryStream del backup y metadatos</returns>
        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackup(int sqlServerDatabaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                // Obtener configuración de SQL Server
                var sqlServerConfig = await _context.SqlServerDataBases.FindAsync(sqlServerDatabaseId);
                if (sqlServerConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de SQL Server no encontrada");
                }

                // Generar nombre de archivo de backup con timestamp
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{sqlServerConfig.DatabaseName}_backup_{timestamp}.bak";

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

                // Configurar permisos de escritura en la carpeta
                EnsureFolderWritePermissions(tempFolder);

                tempBackupPath = Path.Combine(tempFolder, fileName);

                // Construir cadena de conexión
                string connectionString;
                if (sqlServerConfig.IntegratedSecurity)
                {
                    connectionString = $"Server={sqlServerConfig.Host},{sqlServerConfig.Port};Database={sqlServerConfig.DatabaseName};Integrated Security=True;TrustServerCertificate={sqlServerConfig.TrustServerCertificate};";
                }
                else
                {
                    string plainPassword = _credentialProtector.Unprotect(sqlServerConfig.Password) ?? string.Empty;
                    connectionString = $"Server={sqlServerConfig.Host},{sqlServerConfig.Port};Database={sqlServerConfig.DatabaseName};User Id={sqlServerConfig.Username};Password={plainPassword};TrustServerCertificate={sqlServerConfig.TrustServerCertificate};";
                }

                // Ejecutar backup a archivo temporal
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // Comando para realizar el backup. El nombre de la base de datos es un identificador,
                    // no puede parametrizarse con @param; se escapa duplicando los corchetes de cierre.
                    string escapedDatabaseName = sqlServerConfig.DatabaseName.Replace("]", "]]");
                    string backupCommand = $@"
                        BACKUP DATABASE [{escapedDatabaseName}]
                        TO DISK = @backupPath
                        WITH FORMAT,
                             INIT,
                             NAME = @backupName,
                             STATS = 10";

                    using (var command = new SqlCommand(backupCommand, connection))
                    {
                        command.CommandTimeout = 300; // 5 minutos de timeout
                        command.Parameters.AddWithValue("@backupPath", tempBackupPath);
                        command.Parameters.AddWithValue("@backupName", $"{sqlServerConfig.DatabaseName} Backup {timestamp}");

                        await command.ExecuteNonQueryAsync();
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

                return (true, memoryStream, fileName, sqlServerConfig.DatabaseName, string.Empty);
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Error de SQL Server al crear backup de {DatabaseId}", sqlServerDatabaseId);

                // Limpiar archivo temporal si existe
                if (!string.IsNullOrEmpty(tempBackupPath) && System.IO.File.Exists(tempBackupPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempBackupPath);
                    }
                    catch { /* Ignorar errores al eliminar */ }
                }

                return (false, null, string.Empty, string.Empty, $"Error de SQL Server: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear backup de SQL Server {DatabaseId}", sqlServerDatabaseId);

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
    }
}
