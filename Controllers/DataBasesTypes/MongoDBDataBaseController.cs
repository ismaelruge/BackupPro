using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
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

        public MongoDBDataBaseController(ApplicationDbContext context)
        {
            _context = context;
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
                    Password = model.Password, // TODO: Encriptar en producción
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

                // Si no se proporciona contraseña, mantener la actual
                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    model.Password = mongodb.Password;
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
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    mongodb.Password = model.Password; // TODO: Encriptar en producción
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
                    Password = mongodb.Password, // TODO: En producción, no enviar la contraseña o enviarla parcialmente
                    SslEnabled = mongodb.SslEnabled,
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }
    }
}
