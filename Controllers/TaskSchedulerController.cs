using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BackupPro.Controllers
{
    [Authorize]
    public class TaskSchedulerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TaskSchedulerController> _logger;
        private readonly DataBasesTypes.SqlServerDataBaseController _sqlServerController;
        private readonly DataBasesTypes.MySqlDataBaseController _mySqlController;
        private readonly DataBasesTypes.PostgresSqlDataBaseController _postgresSqlController;
        private readonly DataBasesTypes.MongoDBDataBaseController _mongoDBController;
        private readonly StorageTypes.LocalStorageController _localStorageController;
        private readonly StorageTypes.FtpStorageController _ftpStorageController;
        private readonly StorageTypes.BlobStorageController _blobStorageController;
        private readonly StorageTypes.OneDriveStorageController _oneDriveStorageController;
        private readonly StorageTypes.GoogleDriveStorageController _googleDriveStorageController;

        public TaskSchedulerController(
            ApplicationDbContext context,
            ILogger<TaskSchedulerController> logger,
            DataBasesTypes.SqlServerDataBaseController sqlServerController,
            DataBasesTypes.MySqlDataBaseController mySqlController,
            DataBasesTypes.PostgresSqlDataBaseController postgresSqlController,
            DataBasesTypes.MongoDBDataBaseController mongoDBController,
            StorageTypes.LocalStorageController localStorageController,
            StorageTypes.FtpStorageController ftpStorageController,
            StorageTypes.BlobStorageController blobStorageController,
            StorageTypes.OneDriveStorageController oneDriveStorageController,
            StorageTypes.GoogleDriveStorageController googleDriveStorageController)
        {
            _context = context;
            _logger = logger;
            _sqlServerController = sqlServerController;
            _mySqlController = mySqlController;
            _postgresSqlController = postgresSqlController;
            _mongoDBController = mongoDBController;
            _localStorageController = localStorageController;
            _ftpStorageController = ftpStorageController;
            _blobStorageController = blobStorageController;
            _oneDriveStorageController = oneDriveStorageController;
            _googleDriveStorageController = googleDriveStorageController;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.TaskSchedulers.ToListAsync();

            var viewModels = new List<TaskSchedulerIndexViewModel>();

            foreach (var task in tasks)
            {
                var viewModel = new TaskSchedulerIndexViewModel
                {
                    Id = task.Id,
                    TaskName = task.TaskName,
                    DatabaseType = task.DatabaseType,
                    DatabaseId = task.DatabaseId,
                    DatabaseName = await GetDatabaseName(task.DatabaseType, task.DatabaseId),
                    StorageType = task.StorageType,
                    StorageId = task.StorageId,
                    StorageName = await GetStorageName(task.StorageType, task.StorageId),
                    FrequencyType = task.FrequencyType,
                    FrequencyValue = task.FrequencyValue,
                    IsActive = task.IsActive,
                    LastRunAt = task.LastRunAt,
                    NextRunAt = task.NextRunAt,
                    CreatedAt = task.CreatedAt
                };

                viewModels.Add(viewModel);
            }

            return View(viewModels);
        }

        // ========== HELPER METHODS ==========

        /// <summary>
        /// Calcula la próxima fecha de ejecución basada en la frecuencia
        /// </summary>
        private DateTime CalculateNextRun(string frequencyType, int frequencyValue, DateTime? lastRun = null)
        {
            var baseTime = lastRun ?? DateTime.Now;

            return frequencyType.ToLower() switch
            {
                "minutes" => baseTime.AddMinutes(frequencyValue),
                "hours" => baseTime.AddHours(frequencyValue),
                "days" => baseTime.AddDays(frequencyValue),
                _ => baseTime.AddHours(1) // Default: 1 hour
            };
        }

        /// <summary>
        /// Obtiene el nombre de la configuración de base de datos
        /// </summary>
        private async Task<string> GetDatabaseName(string databaseType, int databaseId)
        {
            return databaseType switch
            {
                "SqlServer" => (await _context.SqlServerDataBases.FindAsync(databaseId))?.ConfigurationName ?? "N/A",
                "PostgreSQL" => (await _context.PostgresSqlDataBases.FindAsync(databaseId))?.ConfigurationName ?? "N/A",
                "MySQL" => (await _context.MySqlDataBases.FindAsync(databaseId))?.ConfigurationName ?? "N/A",
                "MongoDB" => (await _context.MongoDBDataBases.FindAsync(databaseId))?.ConfigurationName ?? "N/A",
                _ => "N/A"
            };
        }

        /// <summary>
        /// Obtiene el nombre de la configuración de almacenamiento
        /// </summary>
        private async Task<string> GetStorageName(string storageType, int storageId)
        {
            return storageType switch
            {
                "GoogleDrive" => (await _context.GoogleDriveStorages.FindAsync(storageId))?.ConfigurationName ?? "N/A",
                "OneDrive" => (await _context.OneDriveStorages.FindAsync(storageId))?.ConfigurationName ?? "N/A",
                "AzureBlob" => (await _context.AzureBlobStorages.FindAsync(storageId))?.ConfigurationName ?? "N/A",
                "Ftp" => (await _context.FtpStorages.FindAsync(storageId))?.ConfigurationName ?? "N/A",
                "Local" => (await _context.LocalStorages.FindAsync(storageId))?.ConfigurationName ?? "N/A",
                _ => "N/A"
            };
        }

        // ========== AJAX ENDPOINTS ==========

        /// <summary>
        /// Obtiene la lista de bases de datos según el tipo seleccionado
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDatabasesByType(string type)
        {
            try
            {
                object databases = type switch
                {
                    "SqlServer" => await _context.SqlServerDataBases
                        .Select(d => new { id = d.Id, name = d.ConfigurationName })
                        .ToListAsync(),
                    "PostgreSQL" => await _context.PostgresSqlDataBases
                        .Select(d => new { id = d.Id, name = d.ConfigurationName })
                        .ToListAsync(),
                    "MySQL" => await _context.MySqlDataBases
                        .Select(d => new { id = d.Id, name = d.ConfigurationName })
                        .ToListAsync(),
                    "MongoDB" => await _context.MongoDBDataBases
                        .Select(d => new { id = d.Id, name = d.ConfigurationName })
                        .ToListAsync(),
                    _ => new List<object>()
                };

                return Json(new { success = true, data = databases });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener bases de datos: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene la lista de almacenamientos según el tipo seleccionado
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStoragesByType(string type)
        {
            try
            {
                object storages = type switch
                {
                    "GoogleDrive" => await _context.GoogleDriveStorages
                        .Select(s => new { id = s.Id, name = s.ConfigurationName })
                        .ToListAsync(),
                    "OneDrive" => await _context.OneDriveStorages
                        .Select(s => new { id = s.Id, name = s.ConfigurationName })
                        .ToListAsync(),
                    "AzureBlob" => await _context.AzureBlobStorages
                        .Select(s => new { id = s.Id, name = s.ConfigurationName })
                        .ToListAsync(),
                    "Ftp" => await _context.FtpStorages
                        .Select(s => new { id = s.Id, name = s.ConfigurationName })
                        .ToListAsync(),
                    "Local" => await _context.LocalStorages
                        .Select(s => new { id = s.Id, name = s.ConfigurationName })
                        .ToListAsync(),
                    _ => new List<object>()
                };

                return Json(new { success = true, data = storages });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener almacenamientos: {ex.Message}" });
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva tarea programada
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskSchedulerViewModel model)
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

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si ya existe una tarea con el mismo nombre
                bool exists = await _context.TaskSchedulers.AnyAsync(t => t.TaskName == model.TaskName);

                if (exists)
                {
                    var errorMessage = "Ya existe una tarea con este nombre.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que la base de datos existe
                bool databaseExists = model.DatabaseType switch
                {
                    "SqlServer" => await _context.SqlServerDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "PostgreSQL" => await _context.PostgresSqlDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "MySQL" => await _context.MySqlDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "MongoDB" => await _context.MongoDBDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    _ => false
                };

                if (!databaseExists)
                {
                    var errorMessage = "La base de datos seleccionada no existe.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que el almacenamiento existe
                bool storageExists = model.StorageType switch
                {
                    "GoogleDrive" => await _context.GoogleDriveStorages.AnyAsync(s => s.Id == model.StorageId),
                    "OneDrive" => await _context.OneDriveStorages.AnyAsync(s => s.Id == model.StorageId),
                    "AzureBlob" => await _context.AzureBlobStorages.AnyAsync(s => s.Id == model.StorageId),
                    "Ftp" => await _context.FtpStorages.AnyAsync(s => s.Id == model.StorageId),
                    "Local" => await _context.LocalStorages.AnyAsync(s => s.Id == model.StorageId),
                    _ => false
                };

                if (!storageExists)
                {
                    var errorMessage = "El almacenamiento seleccionado no existe.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                var task = new Models.TaskScheduler
                {
                    TaskName = model.TaskName,
                    DatabaseType = model.DatabaseType,
                    DatabaseId = model.DatabaseId,
                    StorageType = model.StorageType,
                    StorageId = model.StorageId,
                    FrequencyType = model.FrequencyType,
                    FrequencyValue = model.FrequencyValue,
                    IsActive = model.IsActive,
                    NextRunAt = CalculateNextRun(model.FrequencyType, model.FrequencyValue),
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.TaskSchedulers.Add(task);
                await _context.SaveChangesAsync();

                var successMessage = "Tarea programada creada exitosamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear tarea programada");
                var errorMessage = $"Error al crear tarea: {ex.Message}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Error"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Actualiza una tarea programada existente
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TaskSchedulerViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    var errorMessage = "ID de tarea no especificado.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                var task = await _context.TaskSchedulers.FindAsync(model.Id.Value);
                if (task == null)
                {
                    var errorMessage = "Tarea no encontrada.";

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
                bool exists = await _context.TaskSchedulers.AnyAsync(t => t.TaskName == model.TaskName && t.Id != model.Id);
                if (exists)
                {
                    var errorMessage = "Ya existe otra tarea con este nombre.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que la base de datos existe
                bool databaseExists = model.DatabaseType switch
                {
                    "SqlServer" => await _context.SqlServerDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "PostgreSQL" => await _context.PostgresSqlDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "MySQL" => await _context.MySqlDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    "MongoDB" => await _context.MongoDBDataBases.AnyAsync(d => d.Id == model.DatabaseId),
                    _ => false
                };

                if (!databaseExists)
                {
                    var errorMessage = "La base de datos seleccionada no existe.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // Verificar que el almacenamiento existe
                bool storageExists = model.StorageType switch
                {
                    "GoogleDrive" => await _context.GoogleDriveStorages.AnyAsync(s => s.Id == model.StorageId),
                    "OneDrive" => await _context.OneDriveStorages.AnyAsync(s => s.Id == model.StorageId),
                    "AzureBlob" => await _context.AzureBlobStorages.AnyAsync(s => s.Id == model.StorageId),
                    "Ftp" => await _context.FtpStorages.AnyAsync(s => s.Id == model.StorageId),
                    "Local" => await _context.LocalStorages.AnyAsync(s => s.Id == model.StorageId),
                    _ => false
                };

                if (!storageExists)
                {
                    var errorMessage = "El almacenamiento seleccionado no existe.";

                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                    {
                        return Json(new { success = false, message = errorMessage });
                    }

                    TempData["Error"] = errorMessage;
                    return RedirectToAction(nameof(Index));
                }

                task.TaskName = model.TaskName;
                task.DatabaseType = model.DatabaseType;
                task.DatabaseId = model.DatabaseId;
                task.StorageType = model.StorageType;
                task.StorageId = model.StorageId;
                task.FrequencyType = model.FrequencyType;
                task.FrequencyValue = model.FrequencyValue;
                task.IsActive = model.IsActive;

                // Recalcular NextRunAt si cambió la frecuencia
                task.NextRunAt = CalculateNextRun(model.FrequencyType, model.FrequencyValue, task.LastRunAt);
                task.LastModifiedAt = DateTime.Now;

                _context.TaskSchedulers.Update(task);
                await _context.SaveChangesAsync();

                var successMessage = "Tarea actualizada exitosamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar tarea programada");
                var errorMessage = $"Error al actualizar tarea: {ex.Message}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = errorMessage });
                }

                TempData["Error"] = errorMessage;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Elimina una tarea programada
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var task = await _context.TaskSchedulers.FindAsync(id);
                if (task == null)
                {
                    TempData["Error"] = "Tarea no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.TaskSchedulers.Remove(task);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tarea eliminada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar tarea programada");
                TempData["Error"] = $"Error al eliminar tarea: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene una tarea por su ID
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var task = await _context.TaskSchedulers.FindAsync(id);
                if (task == null)
                {
                    return Json(new { success = false, message = "Tarea no encontrada." });
                }

                var viewModel = new TaskSchedulerViewModel
                {
                    Id = task.Id,
                    TaskName = task.TaskName,
                    DatabaseType = task.DatabaseType,
                    DatabaseId = task.DatabaseId,
                    StorageType = task.StorageType,
                    StorageId = task.StorageId,
                    FrequencyType = task.FrequencyType,
                    FrequencyValue = task.FrequencyValue,
                    IsActive = task.IsActive
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener tarea: {ex.Message}" });
            }
        }

        /// <summary>
        /// Activa o desactiva una tarea
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTask(int id)
        {
            try
            {
                var task = await _context.TaskSchedulers.FindAsync(id);
                if (task == null)
                {
                    return Json(new { success = false, message = "Tarea no encontrada." });
                }

                task.IsActive = !task.IsActive;
                task.LastModifiedAt = DateTime.Now;

                _context.TaskSchedulers.Update(task);
                await _context.SaveChangesAsync();

                var status = task.IsActive ? "activada" : "pausada";
                return Json(new { success = true, message = $"Tarea {status} exitosamente.", isActive = task.IsActive });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al cambiar estado de tarea: {ex.Message}" });
            }
        }

        /// <summary>
        /// Registra un error en el histórico de backups
        /// </summary>
        private async Task LogBackupError(int databaseId, string databaseName, DateTime startTime, string errorMessage)
        {
            try
            {
                var backupHistory = new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Error",
                    Message = errorMessage,
                    BackupPath = "N/A"
                };

                _context.BackupHistories.Add(backupHistory);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Ignorar errores al registrar en histórico
            }
        }

        /// <summary>
        /// Ejecuta el backup según el tipo de base de datos y almacenamiento
        /// </summary>
        /// <summary>
        /// Mapa (tipo de base de datos, tipo de almacenamiento) -> función que ejecuta ese backup.
        /// Reemplaza una cadena if/else de 20 ramas por una búsqueda directa, evitando el riesgo de
        /// una combinación mal escrita al agregar/editar una rama.
        /// </summary>
        private Dictionary<(string DatabaseType, string StorageType), Func<int, int, Task<string>>> BuildBackupDispatch() => new()
        {
            [("SqlServer", "Local")] = ExecuteBackupSqlServerLocal,
            [("SqlServer", "Ftp")] = ExecuteBackupSqlServerFtp,
            [("SqlServer", "AzureBlob")] = ExecuteBackupSqlServerBlob,
            [("SqlServer", "OneDrive")] = ExecuteBackupSqlServerOneDrive,
            [("SqlServer", "GoogleDrive")] = ExecuteBackupSqlServerGoogleDrive,

            [("MySQL", "Local")] = ExecuteBackupMySqlLocal,
            [("MySQL", "Ftp")] = ExecuteBackupMySqlFtp,
            [("MySQL", "AzureBlob")] = ExecuteBackupMySqlBlob,
            [("MySQL", "OneDrive")] = ExecuteBackupMySqlOneDrive,
            [("MySQL", "GoogleDrive")] = ExecuteBackupMySqlGoogleDrive,

            [("PostgreSQL", "Local")] = ExecuteBackupPostgreSqlLocal,
            [("PostgreSQL", "Ftp")] = ExecuteBackupPostgreSqlFtp,
            [("PostgreSQL", "AzureBlob")] = ExecuteBackupPostgreSqlBlob,
            [("PostgreSQL", "OneDrive")] = ExecuteBackupPostgreSqlOneDrive,
            [("PostgreSQL", "GoogleDrive")] = ExecuteBackupPostgreSqlGoogleDrive,

            [("MongoDB", "Local")] = ExecuteBackupMongoDBLocal,
            [("MongoDB", "Ftp")] = ExecuteBackupMongoDBFtp,
            [("MongoDB", "AzureBlob")] = ExecuteBackupMongoDBBlob,
            [("MongoDB", "OneDrive")] = ExecuteBackupMongoDBOneDrive,
            [("MongoDB", "GoogleDrive")] = ExecuteBackupMongoDBGoogleDrive,
        };

        private async Task<string> ExecuteBackupByType(Models.TaskScheduler task)
        {
            var dispatch = BuildBackupDispatch();

            if (dispatch.TryGetValue((task.DatabaseType, task.StorageType), out var executeBackup))
            {
                return await executeBackup(task.DatabaseId, task.StorageId);
            }

            return $"Combinación no soportada o aún no implementada: {task.DatabaseType} a {task.StorageType}";
        }

        /// <summary>
        /// Ejecuta backup de SQL Server a almacenamiento local
        /// </summary>
        private async Task<string> ExecuteBackupSqlServerLocal(int sqlServerDatabaseId, int localStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en SQL Server y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _sqlServerController.CreateBackup(sqlServerDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(sqlServerDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento local
                var (saveSuccess, filePath, fileSize, saveError) = await _localStorageController.SaveBackup(
                    localStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    sqlServerDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                // El error ya fue registrado en el histórico si es de CreateBackup
                // o será registrado por SaveBackup si es de almacenamiento
                throw new Exception($"Error al ejecutar backup: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de SQL Server a almacenamiento FTP
        /// </summary>
        private async Task<string> ExecuteBackupSqlServerFtp(int sqlServerDatabaseId, int ftpStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en SQL Server y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _sqlServerController.CreateBackup(sqlServerDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(sqlServerDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento FTP
                var (saveSuccess, filePath, fileSize, saveError) = await _ftpStorageController.SaveBackup(
                    ftpStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    sqlServerDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en FTP: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup FTP: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de SQL Server a Azure Blob Storage
        /// </summary>
        private async Task<string> ExecuteBackupSqlServerBlob(int sqlServerDatabaseId, int blobStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en SQL Server y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _sqlServerController.CreateBackup(sqlServerDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(sqlServerDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Azure Blob Storage
                var (saveSuccess, filePath, fileSize, saveError) = await _blobStorageController.SaveBackup(
                    blobStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    sqlServerDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Azure Blob: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Azure Blob: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de SQL Server a OneDrive
        /// </summary>
        private async Task<string> ExecuteBackupSqlServerOneDrive(int sqlServerDatabaseId, int oneDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en SQL Server y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _sqlServerController.CreateBackup(sqlServerDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(sqlServerDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en OneDrive
                var (saveSuccess, filePath, fileSize, saveError) = await _oneDriveStorageController.SaveBackup(
                    oneDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    sqlServerDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en OneDrive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup OneDrive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de SQL Server a Google Drive
        /// </summary>
        private async Task<string> ExecuteBackupSqlServerGoogleDrive(int sqlServerDatabaseId, int googleDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en SQL Server y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _sqlServerController.CreateBackup(sqlServerDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(sqlServerDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Google Drive
                var (saveSuccess, filePath, fileSize, saveError) = await _googleDriveStorageController.SaveBackup(
                    googleDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    sqlServerDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Google Drive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Google Drive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MySQL a almacenamiento local
        /// </summary>
        private async Task<string> ExecuteBackupMySqlLocal(int mySqlDatabaseId, int localStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en MySQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mySqlController.CreateBackup(mySqlDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mySqlDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento local
                var (saveSuccess, filePath, fileSize, saveError) = await _localStorageController.SaveBackup(
                    localStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mySqlDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MySQL a almacenamiento FTP
        /// </summary>
        private async Task<string> ExecuteBackupMySqlFtp(int mySqlDatabaseId, int ftpStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en MySQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mySqlController.CreateBackup(mySqlDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mySqlDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento FTP
                var (saveSuccess, filePath, fileSize, saveError) = await _ftpStorageController.SaveBackup(
                    ftpStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mySqlDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en FTP: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup FTP: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MySQL a Azure Blob Storage
        /// </summary>
        private async Task<string> ExecuteBackupMySqlBlob(int mySqlDatabaseId, int blobStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en MySQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mySqlController.CreateBackup(mySqlDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mySqlDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Azure Blob Storage
                var (saveSuccess, filePath, fileSize, saveError) = await _blobStorageController.SaveBackup(
                    blobStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mySqlDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Azure Blob: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Azure Blob: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MySQL a OneDrive
        /// </summary>
        private async Task<string> ExecuteBackupMySqlOneDrive(int mySqlDatabaseId, int oneDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en MySQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mySqlController.CreateBackup(mySqlDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mySqlDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en OneDrive
                var (saveSuccess, filePath, fileSize, saveError) = await _oneDriveStorageController.SaveBackup(
                    oneDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mySqlDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en OneDrive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup OneDrive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MySQL a Google Drive
        /// </summary>
        private async Task<string> ExecuteBackupMySqlGoogleDrive(int mySqlDatabaseId, int googleDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en MySQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mySqlController.CreateBackup(mySqlDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mySqlDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Google Drive
                var (saveSuccess, filePath, fileSize, saveError) = await _googleDriveStorageController.SaveBackup(
                    googleDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mySqlDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Google Drive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Google Drive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de PostgreSQL a almacenamiento local
        /// </summary>
        private async Task<string> ExecuteBackupPostgreSqlLocal(int postgresDatabaseId, int localStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en PostgreSQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _postgresSqlController.CreateBackup(postgresDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(postgresDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento local
                var (saveSuccess, filePath, fileSize, saveError) = await _localStorageController.SaveBackup(
                    localStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    postgresDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de PostgreSQL a almacenamiento FTP
        /// </summary>
        private async Task<string> ExecuteBackupPostgreSqlFtp(int postgresDatabaseId, int ftpStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en PostgreSQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _postgresSqlController.CreateBackup(postgresDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(postgresDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en almacenamiento FTP
                var (saveSuccess, filePath, fileSize, saveError) = await _ftpStorageController.SaveBackup(
                    ftpStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    postgresDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en FTP: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup FTP: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de PostgreSQL a Azure Blob Storage
        /// </summary>
        private async Task<string> ExecuteBackupPostgreSqlBlob(int postgresDatabaseId, int blobStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en PostgreSQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _postgresSqlController.CreateBackup(postgresDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(postgresDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Azure Blob Storage
                var (saveSuccess, filePath, fileSize, saveError) = await _blobStorageController.SaveBackup(
                    blobStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    postgresDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Azure Blob: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Azure Blob: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de PostgreSQL a OneDrive
        /// </summary>
        private async Task<string> ExecuteBackupPostgreSqlOneDrive(int postgresDatabaseId, int oneDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en PostgreSQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _postgresSqlController.CreateBackup(postgresDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(postgresDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en OneDrive
                var (saveSuccess, filePath, fileSize, saveError) = await _oneDriveStorageController.SaveBackup(
                    oneDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    postgresDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en OneDrive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup OneDrive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de PostgreSQL a Google Drive
        /// </summary>
        private async Task<string> ExecuteBackupPostgreSqlGoogleDrive(int postgresDatabaseId, int googleDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // Paso 1: Crear el backup en PostgreSQL y obtener el MemoryStream
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _postgresSqlController.CreateBackup(postgresDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(postgresDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // Paso 2: Guardar el backup en Google Drive
                var (saveSuccess, filePath, fileSize, saveError) = await _googleDriveStorageController.SaveBackup(
                    googleDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    postgresDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Google Drive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Google Drive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        // ========== MONGODB BACKUP METHODS ==========

        /// <summary>
        /// Ejecuta backup de MongoDB a almacenamiento local
        /// </summary>
        private async Task<string> ExecuteBackupMongoDBLocal(int mongodbDatabaseId, int localStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // 1. Crear backup de MongoDB
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mongoDBController.CreateBackup(mongodbDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mongodbDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // 2. Guardar en almacenamiento local
                var (saveSuccess, filePath, fileSize, saveError) = await _localStorageController.SaveBackup(
                    localStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mongodbDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MongoDB a almacenamiento FTP
        /// </summary>
        private async Task<string> ExecuteBackupMongoDBFtp(int mongodbDatabaseId, int ftpStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // 1. Crear backup de MongoDB
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mongoDBController.CreateBackup(mongodbDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mongodbDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // 2. Guardar en almacenamiento FTP
                var (saveSuccess, filePath, fileSize, saveError) = await _ftpStorageController.SaveBackup(
                    ftpStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mongodbDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en FTP: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup FTP: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MongoDB a Azure Blob Storage
        /// </summary>
        private async Task<string> ExecuteBackupMongoDBBlob(int mongodbDatabaseId, int blobStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // 1. Crear backup de MongoDB
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mongoDBController.CreateBackup(mongodbDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mongodbDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // 2. Guardar en Azure Blob Storage
                var (saveSuccess, filePath, fileSize, saveError) = await _blobStorageController.SaveBackup(
                    blobStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mongodbDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Azure Blob: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Azure Blob: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MongoDB a OneDrive
        /// </summary>
        private async Task<string> ExecuteBackupMongoDBOneDrive(int mongodbDatabaseId, int oneDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // 1. Crear backup de MongoDB
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mongoDBController.CreateBackup(mongodbDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mongodbDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // 2. Guardar en OneDrive
                var (saveSuccess, filePath, fileSize, saveError) = await _oneDriveStorageController.SaveBackup(
                    oneDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mongodbDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en OneDrive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup OneDrive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Ejecuta backup de MongoDB a Google Drive
        /// </summary>
        private async Task<string> ExecuteBackupMongoDBGoogleDrive(int mongodbDatabaseId, int googleDriveStorageId)
        {
            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;
            string databaseName = "Desconocida";

            try
            {
                // 1. Crear backup de MongoDB
                var (backupSuccess, backupStream2, fileName, dbName, backupError) = await _mongoDBController.CreateBackup(mongodbDatabaseId);
                databaseName = dbName;

                if (!backupSuccess || backupStream2 == null)
                {
                    await LogBackupError(mongodbDatabaseId, databaseName, startTime, backupError);
                    throw new Exception(backupError);
                }

                backupStream = backupStream2;

                // 2. Guardar en Google Drive
                var (saveSuccess, filePath, fileSize, saveError) = await _googleDriveStorageController.SaveBackup(
                    googleDriveStorageId,
                    backupStream,
                    fileName,
                    databaseName,
                    mongodbDatabaseId
                );

                if (!saveSuccess)
                {
                    throw new Exception(saveError);
                }

                return $"Backup creado exitosamente en Google Drive: {fileName} ({FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al ejecutar backup Google Drive: {ex.Message}", ex);
            }
            finally
            {
                // Liberar el MemoryStream
                backupStream?.Dispose();
            }
        }

        /// <summary>
        /// Formatea bytes a una representación legible (KB, MB, GB)
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Ejecuta una tarea específica de manera manual
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteTask(int id)
        {
            try
            {
                var task = await _context.TaskSchedulers.FindAsync(id);
                if (task == null)
                {
                    return Json(new { success = false, message = "Tarea no encontrada." });
                }

                // Validar que la tarea esté activa
                if (!task.IsActive)
                {
                    return Json(new { success = false, message = "No se puede ejecutar una tarea pausada. Activa la tarea primero." });
                }

                // Ejecutar backup según el tipo de base de datos y almacenamiento
                string backupResult = await ExecuteBackupByType(task);

                // Actualizar fechas de la tarea
                task.LastRunAt = DateTime.Now;
                task.NextRunAt = CalculateNextRun(task.FrequencyType, task.FrequencyValue, task.LastRunAt);
                task.LastModifiedAt = DateTime.Now;

                _context.TaskSchedulers.Update(task);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = backupResult });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar tarea {TaskId}", id);
                return Json(new { success = false, message = $"Error al ejecutar tarea: {ex.Message}" });
            }
        }

        /// <summary>
        /// Ejecuta todas las tareas activas de manera manual
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteAllTasks()
        {
            try
            {
                var activeTasks = await _context.TaskSchedulers.Where(t => t.IsActive).ToListAsync();

                if (!activeTasks.Any())
                {
                    return Json(new { success = false, message = "No hay tareas activas para ejecutar." });
                }

                int successCount = 0;
                int errorCount = 0;
                var results = new List<string>();

                foreach (var task in activeTasks)
                {
                    try
                    {
                        // Ejecutar backup según el tipo de base de datos y almacenamiento
                        string backupResult = await ExecuteBackupByType(task);

                        // Actualizar fechas de la tarea
                        task.LastRunAt = DateTime.Now;
                        task.NextRunAt = CalculateNextRun(task.FrequencyType, task.FrequencyValue, task.LastRunAt);
                        task.LastModifiedAt = DateTime.Now;

                        successCount++;
                        results.Add($"{task.TaskName}: {backupResult}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al ejecutar tarea {TaskName} ({TaskId})", task.TaskName, task.Id);
                        errorCount++;
                        results.Add($"{task.TaskName}: Error - {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = errorCount == 0,
                    message = $"{successCount} tarea(s) ejecutada(s) exitosamente. {errorCount} error(es).",
                    details = results
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al ejecutar tareas: {ex.Message}" });
            }
        }
    }
}
