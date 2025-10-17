using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Linq;

namespace BackupPro.Controllers.DataBasesTypes
{
    [Authorize]
    public class SqlServerDataBaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SqlServerDataBaseController(ApplicationDbContext context)
        {
            _context = context;
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
                    Password = model.Password ?? string.Empty, // TODO: Encriptar en producción
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

                // Si no se proporciona contraseña, mantener la actual
                if (string.IsNullOrWhiteSpace(model.Password))
                {
                    model.Password = sqlServer.Password;
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
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    sqlServer.Password = model.Password; // TODO: Encriptar en producción
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
                    Password = sqlServer.Password, // TODO: En producción, no enviar la contraseña o enviarla parcialmente
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
    }
}
