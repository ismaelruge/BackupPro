using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services.Backup;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BackupPro.Controllers
{
    /// <summary>
    /// CRUD de tareas programadas y disparador de ejecución manual. La ejecución en sí (elegir el
    /// provider de base de datos y de almacenamiento correctos y correr el backup) vive en
    /// <see cref="BackupExecutionService"/>, compartido con <see cref="Services.BackupSchedulerBackgroundService"/>
    /// que ejecuta las tareas automáticamente cuando llega su hora.
    /// </summary>
    [Authorize]
    public class TaskSchedulerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BackupExecutionService _backupExecutionService;
        private readonly ILogger<TaskSchedulerController> _logger;

        public TaskSchedulerController(ApplicationDbContext context, BackupExecutionService backupExecutionService, ILogger<TaskSchedulerController> logger)
        {
            _context = context;
            _backupExecutionService = backupExecutionService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.TaskSchedulers.ToListAsync();

            // Se cargan los nombres de configuración con una consulta por tipo (filtrada a los ids
            // realmente referenciados), en vez de una consulta por cada tarea (N+1).
            var databaseNames = await LoadDatabaseNamesAsync(tasks);
            var storageNames = await LoadStorageNamesAsync(tasks);

            var viewModels = tasks.Select(task => new TaskSchedulerIndexViewModel
            {
                Id = task.Id,
                TaskName = task.TaskName,
                DatabaseType = task.DatabaseType,
                DatabaseId = task.DatabaseId,
                DatabaseName = databaseNames.GetValueOrDefault((task.DatabaseType, task.DatabaseId), "N/A"),
                StorageType = task.StorageType,
                StorageId = task.StorageId,
                StorageName = storageNames.GetValueOrDefault((task.StorageType, task.StorageId), "N/A"),
                FrequencyType = task.FrequencyType,
                FrequencyValue = task.FrequencyValue,
                IsActive = task.IsActive,
                LastRunAt = task.LastRunAt,
                NextRunAt = task.NextRunAt,
                CreatedAt = task.CreatedAt
            }).ToList();

            return View(viewModels);
        }

        private async Task<Dictionary<(string Type, int Id), string>> LoadDatabaseNamesAsync(List<Models.TaskScheduler> tasks)
        {
            var names = new Dictionary<(string, int), string>();

            var sqlServerIds = IdsFor(tasks, "SqlServer");
            if (sqlServerIds.Count > 0)
            {
                await foreach (var d in _context.SqlServerDataBases.Where(d => sqlServerIds.Contains(d.Id)).AsAsyncEnumerable())
                    names[("SqlServer", d.Id)] = d.ConfigurationName;
            }

            var mySqlIds = IdsFor(tasks, "MySQL");
            if (mySqlIds.Count > 0)
            {
                await foreach (var d in _context.MySqlDataBases.Where(d => mySqlIds.Contains(d.Id)).AsAsyncEnumerable())
                    names[("MySQL", d.Id)] = d.ConfigurationName;
            }

            var postgresIds = IdsFor(tasks, "PostgreSQL");
            if (postgresIds.Count > 0)
            {
                await foreach (var d in _context.PostgresSqlDataBases.Where(d => postgresIds.Contains(d.Id)).AsAsyncEnumerable())
                    names[("PostgreSQL", d.Id)] = d.ConfigurationName;
            }

            var mongoIds = IdsFor(tasks, "MongoDB");
            if (mongoIds.Count > 0)
            {
                await foreach (var d in _context.MongoDBDataBases.Where(d => mongoIds.Contains(d.Id)).AsAsyncEnumerable())
                    names[("MongoDB", d.Id)] = d.ConfigurationName;
            }

            return names;
        }

        private async Task<Dictionary<(string Type, int Id), string>> LoadStorageNamesAsync(List<Models.TaskScheduler> tasks)
        {
            var names = new Dictionary<(string, int), string>();

            var googleDriveIds = IdsFor(tasks, "GoogleDrive", task => task.StorageType, task => task.StorageId);
            if (googleDriveIds.Count > 0)
            {
                await foreach (var s in _context.GoogleDriveStorages.Where(s => googleDriveIds.Contains(s.Id)).AsAsyncEnumerable())
                    names[("GoogleDrive", s.Id)] = s.ConfigurationName;
            }

            var oneDriveIds = IdsFor(tasks, "OneDrive", task => task.StorageType, task => task.StorageId);
            if (oneDriveIds.Count > 0)
            {
                await foreach (var s in _context.OneDriveStorages.Where(s => oneDriveIds.Contains(s.Id)).AsAsyncEnumerable())
                    names[("OneDrive", s.Id)] = s.ConfigurationName;
            }

            var blobIds = IdsFor(tasks, "AzureBlob", task => task.StorageType, task => task.StorageId);
            if (blobIds.Count > 0)
            {
                await foreach (var s in _context.AzureBlobStorages.Where(s => blobIds.Contains(s.Id)).AsAsyncEnumerable())
                    names[("AzureBlob", s.Id)] = s.ConfigurationName;
            }

            var ftpIds = IdsFor(tasks, "Ftp", task => task.StorageType, task => task.StorageId);
            if (ftpIds.Count > 0)
            {
                await foreach (var s in _context.FtpStorages.Where(s => ftpIds.Contains(s.Id)).AsAsyncEnumerable())
                    names[("Ftp", s.Id)] = s.ConfigurationName;
            }

            var localIds = IdsFor(tasks, "Local", task => task.StorageType, task => task.StorageId);
            if (localIds.Count > 0)
            {
                await foreach (var s in _context.LocalStorages.Where(s => localIds.Contains(s.Id)).AsAsyncEnumerable())
                    names[("Local", s.Id)] = s.ConfigurationName;
            }

            return names;
        }

        private static HashSet<int> IdsFor(List<Models.TaskScheduler> tasks, string type) =>
            IdsFor(tasks, type, task => task.DatabaseType, task => task.DatabaseId);

        private static HashSet<int> IdsFor(List<Models.TaskScheduler> tasks, string type, Func<Models.TaskScheduler, string> typeSelector, Func<Models.TaskScheduler, int> idSelector) =>
            tasks.Where(t => typeSelector(t) == type).Select(idSelector).ToHashSet();

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
                    NextRunAt = BackupFrequencyCalculator.CalculateNextRun(model.FrequencyType, model.FrequencyValue, DateTime.Now),
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
                task.NextRunAt = BackupFrequencyCalculator.CalculateNextRun(model.FrequencyType, model.FrequencyValue, task.LastRunAt ?? DateTime.Now);
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
                string backupResult = await _backupExecutionService.ExecuteAsync(task);

                // Actualizar fechas de la tarea
                var now = DateTime.Now;
                task.LastRunAt = now;
                task.NextRunAt = BackupFrequencyCalculator.CalculateNextRun(task.FrequencyType, task.FrequencyValue, now);
                task.LastModifiedAt = now;

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
                        string backupResult = await _backupExecutionService.ExecuteAsync(task);

                        // Actualizar fechas de la tarea
                        var now = DateTime.Now;
                        task.LastRunAt = now;
                        task.NextRunAt = BackupFrequencyCalculator.CalculateNextRun(task.FrequencyType, task.FrequencyValue, now);
                        task.LastModifiedAt = now;

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
