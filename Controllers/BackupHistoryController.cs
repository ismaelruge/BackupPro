using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers
{
    [Authorize]
    public class BackupHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BackupHistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Vista principal del historial de backups
        /// </summary>
        public async Task<IActionResult> Index(string? status, string? search, int page = 1, int pageSize = 20)
        {
            try
            {
                var query = _context.BackupHistories.AsQueryable();

                // Filtrar por estado si se especifica
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(h => h.Status == status);
                }

                // Buscar por nombre de base de datos (ignorar mayúsculas/minúsculas)
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(h => h.DatabaseName.ToLower().Contains(search.ToLower()));
                }

                // Ordenar por fecha descendente (más recientes primero)
                query = query.OrderByDescending(h => h.Date);

                // Contar total de registros
                var totalRecords = await query.CountAsync();

                // Aplicar paginación
                var histories = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Calcular información de paginación
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                ViewBag.TotalRecords = totalRecords;
                ViewBag.Status = status;
                ViewBag.Search = search;

                // Estadísticas generales
                ViewBag.TotalBackups = await _context.BackupHistories.CountAsync();
                ViewBag.SuccessfulBackups = await _context.BackupHistories.CountAsync(h => h.Status == "Exitoso");
                ViewBag.FailedBackups = await _context.BackupHistories.CountAsync(h => h.Status == "Error");

                return View(histories);
            }
            catch (Exception ex)
            {
                return View(new List<BackupHistory>());
            }
        }

        /// <summary>
        /// Obtiene los detalles de un historial específico
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var history = await _context.BackupHistories.FindAsync(id);

                if (history == null)
                {
                    return Json(new { success = false, message = "Historial no encontrado." });
                }

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = history.Id,
                        databaseName = history.DatabaseName,
                        date = history.Date.ToString("dd/MM/yyyy hh:mm:ss tt"),
                        status = history.Status,
                        message = history.Message,
                        backupPath = history.BackupPath
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina un registro del historial
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var history = await _context.BackupHistories.FindAsync(id);

                if (history == null)
                {
                    return Json(new { success = false, message = "Historial no encontrado." });
                }

                _context.BackupHistories.Remove(history);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Registro eliminado exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Limpia todo el historial
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ClearAll()
        {
            try
            {
                var allHistories = await _context.BackupHistories.ToListAsync();
                _context.BackupHistories.RemoveRange(allHistories);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Historial limpiado exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas del historial
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetStatistics()
        {
            try
            {
                var totalBackups = await _context.BackupHistories.CountAsync();
                var successfulBackups = await _context.BackupHistories.CountAsync(h => h.Status == "Exitoso");
                var failedBackups = await _context.BackupHistories.CountAsync(h => h.Status == "Error");

                var lastBackup = await _context.BackupHistories
                    .OrderByDescending(h => h.Date)
                    .FirstOrDefaultAsync();

                var topDatabases = await _context.BackupHistories
                    .GroupBy(h => h.DatabaseName)
                    .Select(g => new { DatabaseName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(5)
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        totalBackups,
                        successfulBackups,
                        failedBackups,
                        successRate = totalBackups > 0 ? (successfulBackups * 100.0 / totalBackups) : 0,
                        lastBackupDate = lastBackup?.Date.ToString("dd/MM/yyyy HH:mm:ss"),
                        topDatabases
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
