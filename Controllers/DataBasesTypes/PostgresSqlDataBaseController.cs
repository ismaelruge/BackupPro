using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

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

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de PostgreSQL.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PostgresSqlDataBaseViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.PostgresSqlDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var postgres = new PostgresSqlDataBase
                    {
                        ConfigurationName = model.ConfigurationName,
                        Host = model.Host,
                        Port = model.Port,
                        DatabaseName = model.DatabaseName,
                        Username = model.Username,
                        Password = model.Password, // TODO: Encriptar en producción
                        SslMode = model.SslMode,
                        SearchPath = model.SearchPath,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.PostgresSqlDataBases.Add(postgres);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de PostgreSQL creada exitosamente.", id = postgres.Id });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de PostgreSQL existente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] PostgresSqlDataBaseViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var postgres = await _context.PostgresSqlDataBases.FindAsync(model.Id.Value);
                if (postgres == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.PostgresSqlDataBases.AnyAsync(p => p.ConfigurationName == model.ConfigurationName && p.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                postgres.ConfigurationName = model.ConfigurationName;
                postgres.Host = model.Host;
                postgres.Port = model.Port;
                postgres.DatabaseName = model.DatabaseName;
                postgres.Username = model.Username;
                postgres.Password = model.Password; // TODO: Encriptar en producción
                postgres.SslMode = model.SslMode;
                postgres.SearchPath = model.SearchPath;
                postgres.LastModifiedAt = DateTime.Now;

                _context.PostgresSqlDataBases.Update(postgres);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de PostgreSQL.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var postgres = await _context.PostgresSqlDataBases.FindAsync(id);
                if (postgres == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.PostgresSqlDataBases.Remove(postgres);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
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
                    SslMode = postgres.SslMode,
                    SearchPath = postgres.SearchPath
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
