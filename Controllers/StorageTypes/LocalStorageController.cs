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
using Microsoft.Data.SqlClient;
using BackupPro.Models;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Security.Principal;

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

        /// <summary>
        /// Configura permisos de escritura completos en la carpeta
        /// </summary>
        private bool EnsureFolderWritePermissions(string folderPath)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(folderPath);
                var directorySecurity = directoryInfo.GetAccessControl();

                // Agregar permisos de escritura completos para Everyone
                var fileSystemRule = new FileSystemAccessRule(
                    "Everyone",
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                directorySecurity.AddAccessRule(fileSystemRule);
                directoryInfo.SetAccessControl(directorySecurity);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Guarda un backup (MemoryStream) en el almacenamiento local
        /// </summary>
        /// <param name="localStorageId">ID de la configuración de almacenamiento local</param>
        /// <param name="backupStream">Stream del backup</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="databaseName">Nombre de la base de datos</param>
        /// <param name="databaseId">ID de la base de datos de origen</param>
        /// <returns>Tuple con el resultado de la operación</returns>
        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackup(int localStorageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string backupFilePath = string.Empty;

            try
            {
                // Obtener configuración de almacenamiento local
                var localStorage = await _context.LocalStorages.FindAsync(localStorageId);
                if (localStorage == null)
                {
                    return (false, string.Empty, 0, "Configuración de almacenamiento local no encontrada");
                }

                // Verificar que la carpeta de destino existe
                if (!Directory.Exists(localStorage.FolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(localStorage.FolderPath);
                    }
                    catch (Exception ex)
                    {
                        return (false, string.Empty, 0, $"No se pudo crear la carpeta de destino: {ex.Message}");
                    }
                }

                // Configurar permisos de escritura en la carpeta de destino
                EnsureFolderWritePermissions(localStorage.FolderPath);

                // Cambiar extensión a .zip
                string zipFileName = Path.ChangeExtension(fileName, ".zip");
                backupFilePath = Path.Combine(localStorage.FolderPath, zipFileName);

                // Comprimir el backup en un archivo .zip
                using (var fileStream = new FileStream(backupFilePath, FileMode.Create, FileAccess.Write))
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    {
                        backupStream.Position = 0; // Asegurar que el stream está al inicio
                        await backupStream.CopyToAsync(entryStream);
                    }
                }

                // Verificar que el archivo se creó correctamente
                if (!System.IO.File.Exists(backupFilePath))
                {
                    return (false, string.Empty, 0, "El archivo de backup no se creó correctamente");
                }

                var fileInfo = new FileInfo(backupFilePath);
                var duration = DateTime.Now - startTime;

                // Registrar en el histórico
                var backupHistory = new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Exitoso",
                    Message = $"Backup guardado exitosamente. Tamaño: {FormatBytes(fileInfo.Length)}. Duración: {duration.TotalSeconds:F2} segundos.",
                    BackupPath = backupFilePath
                };

                _context.BackupHistories.Add(backupHistory);
                await _context.SaveChangesAsync();

                return (true, backupFilePath, fileInfo.Length, string.Empty);
            }
            catch (Exception ex)
            {
                // Error general
                var errorMessage = $"Error al guardar backup: {ex.Message}";

                // Intentar eliminar archivo de backup incompleto si existe
                if (!string.IsNullOrEmpty(backupFilePath) && System.IO.File.Exists(backupFilePath))
                {
                    try
                    {
                        System.IO.File.Delete(backupFilePath);
                    }
                    catch { /* Ignorar errores al eliminar */ }
                }

                // Registrar error en el histórico
                try
                {
                    var backupHistory = new BackupHistory
                    {
                        DatabaseSourceId = databaseId,
                        DatabaseName = databaseName,
                        Date = startTime,
                        Status = "Error",
                        Message = errorMessage,
                        BackupPath = backupFilePath ?? "N/A"
                    };

                    _context.BackupHistories.Add(backupHistory);
                    await _context.SaveChangesAsync();
                }
                catch { /* Ignorar errores al registrar */ }

                return (false, string.Empty, 0, errorMessage);
            }
        }

        /// <summary>
        /// Formatea bytes a una representación legible (KB, MB, GB)
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
