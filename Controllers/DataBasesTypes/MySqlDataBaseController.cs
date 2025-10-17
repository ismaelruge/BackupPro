using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

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

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de MySQL.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MySqlDataBaseViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.MySqlDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var mysql = new MySqlDataBase
                    {
                        ConfigurationName = model.ConfigurationName,
                        Host = model.Host,
                        Port = model.Port,
                        DatabaseName = model.DatabaseName,
                        Username = model.Username,
                        Password = model.Password, // TODO: Encriptar en producción
                        SslMode = model.SslMode,
                        Charset = model.Charset,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.MySqlDataBases.Add(mysql);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de MySQL creada exitosamente.", id = mysql.Id });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de MySQL existente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] MySqlDataBaseViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var mysql = await _context.MySqlDataBases.FindAsync(model.Id.Value);
                if (mysql == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.MySqlDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName && m.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                mysql.ConfigurationName = model.ConfigurationName;
                mysql.Host = model.Host;
                mysql.Port = model.Port;
                mysql.DatabaseName = model.DatabaseName;
                mysql.Username = model.Username;
                mysql.Password = model.Password; // TODO: Encriptar en producción
                mysql.SslMode = model.SslMode;
                mysql.Charset = model.Charset;
                mysql.LastModifiedAt = DateTime.Now;

                _context.MySqlDataBases.Update(mysql);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de MySQL.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var mysql = await _context.MySqlDataBases.FindAsync(id);
                if (mysql == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.MySqlDataBases.Remove(mysql);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
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
                    SslMode = mysql.SslMode,
                    Charset = mysql.Charset
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
