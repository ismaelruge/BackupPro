using BackupPro.Data;
using BackupPro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LocalStorageModel = BackupPro.Models.LocalStorage;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class LocalStorageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LocalStorageController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: LocalStorage
        public async Task<IActionResult> Index()
        {
            var configurations = await _context.LocalStorages
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(configurations);
        }

        // GET: LocalStorage/GetAll
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var configurations = await _context.LocalStorages
                    .OrderByDescending(x => x.CreatedAt)
                    .ToListAsync();

                var viewModels = configurations.Select(c => new LocalStorageViewModel
                {
                    Id = c.Id,
                    ConfigurationName = c.ConfigurationName,
                    FolderPath = c.FolderPath,
                    IsAccessible = Directory.Exists(c.FolderPath)
                }).ToList();

                return Json(new { success = true, data = viewModels });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: LocalStorage/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] LocalStorageViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                // Validar que la carpeta existe
                if (!Directory.Exists(model.FolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(model.FolderPath);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"No se pudo crear la carpeta: {ex.Message}" });
                    }
                }

                var localStorage = new LocalStorageModel
                {
                    ConfigurationName = model.ConfigurationName,
                    FolderPath = model.FolderPath,
                    CreatedAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name
                };

                _context.LocalStorages.Add(localStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración de almacenamiento local creada exitosamente", data = localStorage });
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error al crear configuración de almacenamiento local");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: LocalStorage/GetById/5
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var localStorage = await _context.LocalStorages.FindAsync(id);
                if (localStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada" });
                }

                var viewModel = new LocalStorageViewModel
                {
                    Id = localStorage.Id,
                    ConfigurationName = localStorage.ConfigurationName,
                    FolderPath = localStorage.FolderPath,
                    IsAccessible = Directory.Exists(localStorage.FolderPath)
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: LocalStorage/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromBody] LocalStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no válido" });
                }

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return Json(new { success = false, message = string.Join(", ", errors) });
                }

                var localStorage = await _context.LocalStorages.FindAsync(model.Id.Value);
                if (localStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada" });
                }

                // Validar que la nueva carpeta existe
                if (!Directory.Exists(model.FolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(model.FolderPath);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"No se pudo crear la carpeta: {ex.Message}" });
                    }
                }

                localStorage.ConfigurationName = model.ConfigurationName;
                localStorage.FolderPath = model.FolderPath;
                localStorage.LastModifiedAt = DateTime.Now;

                _context.LocalStorages.Update(localStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente", data = localStorage });
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error al actualizar configuración de almacenamiento local");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: LocalStorage/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, bool deleteFiles = false)
        {
            try
            {
                var localStorage = await _context.LocalStorages.FindAsync(id);
                if (localStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada" });
                }

                // Si se solicita eliminar archivos, intentar eliminar la carpeta
                if (deleteFiles && Directory.Exists(localStorage.FolderPath))
                {
                    try
                    {
                        Directory.Delete(localStorage.FolderPath, true);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"Configuración eliminada pero no se pudo eliminar la carpeta: {ex.Message}" });
                    }
                }

                _context.LocalStorages.Remove(localStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
