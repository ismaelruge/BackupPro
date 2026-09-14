using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using Oracle.ManagedDataAccess.Client;
using System.Linq;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class OracleDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<OracleDataBaseController> _logger;

        public OracleDataBaseController(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<OracleDataBaseController> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.OracleDataBases.ToListAsync();
            return View(list);
        }

        // ========== CONNECTION VALIDATION ==========

        /// <summary>
        /// Prueba la conexión a Oracle con los parámetros proporcionados.
        /// </summary>
        private async Task<(bool success, string message)> TestConnection(OracleDataBaseViewModel model)
        {
            try
            {
                // Oracle.ManagedDataAccess usa el formato EZCONNECT (host:port/service_name) en vez de
                // parámetros sueltos de host/puerto/base como los demás proveedores.
                var connectionStringBuilder = new OracleConnectionStringBuilder
                {
                    DataSource = $"{model.Host}:{model.Port}/{model.ServiceName}",
                    UserID = model.Username,
                    Password = model.Password,
                    ConnectionTimeout = 10 // 10 segundos de timeout
                };

                using (var connection = new OracleConnection(connectionStringBuilder.ConnectionString))
                {
                    await connection.OpenAsync();

                    // Ejecutar un query simple para verificar que la conexión funciona completamente
                    using (var command = new OracleCommand("SELECT 1 FROM DUAL", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }

                    return (true, "Conexión exitosa a Oracle.");
                }
            }
            catch (OracleException ex)
            {
                return (false, $"Error de Oracle: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Error al conectar: {ex.Message}");
            }
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de Oracle.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OracleDataBaseViewModel model)
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
                bool exists = await _context.OracleDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName);

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

                var oracle = new OracleDataBase
                {
                    ConfigurationName = model.ConfigurationName,
                    Host = model.Host,
                    Port = model.Port,
                    ServiceName = model.ServiceName,
                    Username = model.Username ?? string.Empty,
                    Password = _credentialProtector.Protect(model.Password) ?? string.Empty,
                    DumpDirectoryPath = model.DumpDirectoryPath,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.OracleDataBases.Add(oracle);
                await _context.SaveChangesAsync();

                var successMessage = "Configuración de Oracle creada exitosamente. La conexión fue validada correctamente.";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
                {
                    return Json(new { success = true, message = successMessage });
                }

                TempData["Success"] = successMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de Oracle");
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
        /// Actualiza una configuración de Oracle existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OracleDataBaseViewModel model)
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

                var oracle = await _context.OracleDataBases.FindAsync(model.Id.Value);
                if (oracle == null)
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
                bool passwordProvided = !string.IsNullOrWhiteSpace(model.Password);
                if (!passwordProvided && string.IsNullOrWhiteSpace(oracle.Password))
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
                bool exists = await _context.OracleDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName && p.Id != model.Id);
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
                if (!passwordProvided)
                {
                    model.Password = _credentialProtector.Unprotect(oracle.Password);
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

                oracle.ConfigurationName = model.ConfigurationName;
                oracle.Host = model.Host;
                oracle.Port = model.Port;
                oracle.ServiceName = model.ServiceName;
                oracle.Username = model.Username ?? string.Empty;

                // Solo actualizar la contraseña si se proporciona una nueva
                if (passwordProvided)
                {
                    oracle.Password = _credentialProtector.Protect(model.Password) ?? string.Empty;
                }

                oracle.DumpDirectoryPath = model.DumpDirectoryPath;
                oracle.LastModifiedAt = DateTime.Now;

                _context.OracleDataBases.Update(oracle);
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
                _logger.LogError(ex, "Error al actualizar configuración de Oracle");
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
        /// Elimina una configuración de Oracle.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var oracle = await _context.OracleDataBases.FindAsync(id);
                if (oracle == null)
                {
                    TempData["Error"] = "Configuración no encontrada.";
                    return RedirectToAction(nameof(Index));
                }

                _context.OracleDataBases.Remove(oracle);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Configuración eliminada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de Oracle");
                TempData["Error"] = $"Error al eliminar configuración: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// Obtiene una configuración de Oracle por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var oracle = await _context.OracleDataBases.FindAsync(id);
                if (oracle == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new OracleDataBaseViewModel
                {
                    Id = oracle.Id,
                    ConfigurationName = oracle.ConfigurationName,
                    Host = oracle.Host,
                    Port = oracle.Port,
                    ServiceName = oracle.ServiceName,
                    Username = oracle.Username,
                    Password = string.Empty, // La contraseña nunca se envía al cliente; dejar en blanco para no cambiarla
                    DumpDirectoryPath = oracle.DumpDirectoryPath
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
