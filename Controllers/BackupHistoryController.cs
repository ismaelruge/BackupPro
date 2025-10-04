using BackupPro.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador para gestionar el historial de backups.
    /// </summary>
    [Authorize]
    public class BackupHistoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        /// <summary>
        /// Inicializa una nueva instancia del <see cref="BackupHistoryController"/>.
        /// </summary>
        /// <param name="context">Contexto de base de datos de la aplicación.</param>
        public BackupHistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lista los registros de historial de backups con paginación y búsqueda opcional.
        /// </summary>
        /// <param name="page">Página actual (por defecto 1).</param>
        /// <param name="search">Texto de búsqueda opcional.</param>
        /// <returns>Vista con la lista paginada de backups.</returns>
        public async Task<IActionResult> Index(int page = 1, string? search = null)
        {
            const int pageSize = 10;
            var query = _context.BackupHistories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(h => h.DatabaseName.Contains(search) || h.Status.Contains(search) || h.Message.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(h => h.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new BackupPro.ViewModels.BackupHistoryPagedViewModel
            {
                Items = items,
                PageIndex = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Search = search ?? string.Empty
            };
            return View(model);
        }
    }
}
