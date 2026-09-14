using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers.StorageTypes
{
    /// <summary>
    /// Controlador para la gestión de configuraciones de Amazon S3.
    /// </summary>
    [Authorize]
    public class S3StorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<S3StorageController> _logger;

        public S3StorageController(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<S3StorageController> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        #region Métodos CRUD

        /// <summary>
        /// Lista todas las configuraciones de Amazon S3 registradas.
        /// </summary>
        /// <returns>Vista con la lista de configuraciones.</returns>
        public async Task<IActionResult> Index()
        {
            var list = await _context.S3Storages.ToListAsync();
            return View(list);
        }

        /// <summary>
        /// Procesa la creación de una nueva configuración de Amazon S3.
        /// </summary>
        /// <param name="model">Datos de la configuración a crear.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] S3StorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.S3Storages.AnyAsync(s => s.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var s3Storage = new S3Storage
                    {
                        ConfigurationName = model.ConfigurationName,
                        AccessKeyId = model.AccessKeyId,
                        SecretAccessKey = _credentialProtector.Protect(model.SecretAccessKey) ?? string.Empty,
                        BucketName = model.BucketName,
                        Region = model.Region,
                        Prefix = model.Prefix ?? string.Empty,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.S3Storages.Add(s3Storage);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de Amazon S3 creada exitosamente." });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de Amazon S3");
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Procesa la edición de una configuración de Amazon S3.
        /// </summary>
        /// <param name="model">Datos editados de la configuración.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] S3StorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var s3Storage = await _context.S3Storages.FindAsync(model.Id.Value);
                if (s3Storage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.S3Storages.AnyAsync(s => s.ConfigurationName == model.ConfigurationName && s.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                s3Storage.ConfigurationName = model.ConfigurationName;
                s3Storage.AccessKeyId = model.AccessKeyId;

                // Solo actualizar el Secret Access Key si se proporciona uno nuevo (el cliente nunca
                // recibe el real, así que un valor en blanco significa "no cambiar")
                if (!string.IsNullOrWhiteSpace(model.SecretAccessKey))
                {
                    s3Storage.SecretAccessKey = _credentialProtector.Protect(model.SecretAccessKey) ?? string.Empty;
                }

                s3Storage.BucketName = model.BucketName;
                s3Storage.Region = model.Region;
                s3Storage.Prefix = model.Prefix ?? string.Empty;
                s3Storage.LastModifiedAt = DateTime.Now;

                _context.S3Storages.Update(s3Storage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración de Amazon S3");
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de Amazon S3 por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración a eliminar.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var s3Storage = await _context.S3Storages.FindAsync(id);
                if (s3Storage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.S3Storages.Remove(s3Storage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de Amazon S3");
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los datos de una configuración de Amazon S3 por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración.</param>
        /// <returns>Resultado JSON con los datos de la configuración.</returns>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var s3Storage = await _context.S3Storages.FindAsync(id);
                if (s3Storage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new S3StorageViewModel
                {
                    Id = s3Storage.Id,
                    ConfigurationName = s3Storage.ConfigurationName,
                    AccessKeyId = s3Storage.AccessKeyId,
                    SecretAccessKey = string.Empty, // El Secret Access Key nunca se envía al cliente; dejar en blanco para no cambiarlo
                    BucketName = s3Storage.BucketName,
                    Region = s3Storage.Region,
                    Prefix = s3Storage.Prefix
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        #endregion
    }
}
