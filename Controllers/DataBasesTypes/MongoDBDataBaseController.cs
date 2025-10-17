using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

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

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de MongoDB.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MongoDBDataBaseViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.MongoDBDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var mongodb = new MongoDBDataBase
                    {
                        ConfigurationName = model.ConfigurationName,
                        Host = model.Host,
                        Port = model.Port,
                        DatabaseName = model.DatabaseName,
                        Username = model.Username,
                        Password = model.Password, // TODO: Encriptar en producción
                        AuthenticationDatabase = model.AuthenticationDatabase,
                        SslEnabled = model.SslEnabled,
                        ReplicaSet = model.ReplicaSet,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.MongoDBDataBases.Add(mongodb);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de MongoDB creada exitosamente.", id = mongodb.Id });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de MongoDB existente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] MongoDBDataBaseViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var mongodb = await _context.MongoDBDataBases.FindAsync(model.Id.Value);
                if (mongodb == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.MongoDBDataBases.AnyAsync(m => m.ConfigurationName == model.ConfigurationName && m.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                mongodb.ConfigurationName = model.ConfigurationName;
                mongodb.Host = model.Host;
                mongodb.Port = model.Port;
                mongodb.DatabaseName = model.DatabaseName;
                mongodb.Username = model.Username;
                mongodb.Password = model.Password; // TODO: Encriptar en producción
                mongodb.AuthenticationDatabase = model.AuthenticationDatabase;
                mongodb.SslEnabled = model.SslEnabled;
                mongodb.ReplicaSet = model.ReplicaSet;
                mongodb.LastModifiedAt = DateTime.Now;

                _context.MongoDBDataBases.Update(mongodb);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de MongoDB.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var mongodb = await _context.MongoDBDataBases.FindAsync(id);
                if (mongodb == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.MongoDBDataBases.Remove(mongodb);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
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
                    AuthenticationDatabase = mongodb.AuthenticationDatabase,
                    SslEnabled = mongodb.SslEnabled,
                    ReplicaSet = mongodb.ReplicaSet
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
