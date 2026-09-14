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
    /// Controlador para la gestión de configuraciones de almacenamiento SFTP.
    /// </summary>
    [Authorize]
    public class SftpStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<SftpStorageController> _logger;

        public SftpStorageController(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<SftpStorageController> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        #region Métodos CRUD

        /// <summary>
        /// Lista todas las configuraciones de SFTP registradas.
        /// </summary>
        /// <returns>Vista con la lista de configuraciones.</returns>
        public async Task<IActionResult> Index()
        {
            var list = await _context.SftpStorages.ToListAsync();
            return View(list);
        }

        /// <summary>
        /// Procesa la creación de una nueva configuración SFTP.
        /// </summary>
        /// <param name="model">Datos de la configuración a crear.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] SftpStorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.SftpStorages.AnyAsync(f => f.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var sftpStorage = new SftpStorage
                    {
                        ConfigurationName = model.ConfigurationName,
                        Host = model.Host,
                        Port = model.Port,
                        Username = model.Username,
                        Password = _credentialProtector.Protect(model.Password) ?? string.Empty,
                        RemotePath = model.RemotePath,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.SftpStorages.Add(sftpStorage);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración SFTP creada exitosamente." });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración SFTP");
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Procesa la edición de una configuración SFTP.
        /// </summary>
        /// <param name="model">Datos editados de la configuración.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] SftpStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var sftpStorage = await _context.SftpStorages.FindAsync(model.Id.Value);
                if (sftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.SftpStorages.AnyAsync(f => f.ConfigurationName == model.ConfigurationName && f.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                sftpStorage.ConfigurationName = model.ConfigurationName;
                sftpStorage.Host = model.Host;
                sftpStorage.Port = model.Port;
                sftpStorage.Username = model.Username;

                // Solo actualizar la contraseña si se proporciona una nueva (el cliente nunca recibe la
                // contraseña real, así que un valor en blanco significa "no cambiar")
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    sftpStorage.Password = _credentialProtector.Protect(model.Password) ?? string.Empty;
                }

                sftpStorage.RemotePath = model.RemotePath;
                sftpStorage.LastModifiedAt = DateTime.Now;

                _context.SftpStorages.Update(sftpStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración SFTP");
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración SFTP por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración a eliminar.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var sftpStorage = await _context.SftpStorages.FindAsync(id);
                if (sftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.SftpStorages.Remove(sftpStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración SFTP");
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los datos de una configuración SFTP por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración.</param>
        /// <returns>Resultado JSON con los datos de la configuración.</returns>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var sftpStorage = await _context.SftpStorages.FindAsync(id);
                if (sftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new SftpStorageViewModel
                {
                    Id = sftpStorage.Id,
                    ConfigurationName = sftpStorage.ConfigurationName,
                    Host = sftpStorage.Host,
                    Port = sftpStorage.Port,
                    Username = sftpStorage.Username,
                    Password = string.Empty, // La contraseña nunca se envía al cliente; dejar en blanco para no cambiarla
                    RemotePath = sftpStorage.RemotePath
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
