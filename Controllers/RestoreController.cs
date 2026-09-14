using BackupPro.Data;
using BackupPro.Services.Backup;
using BackupPro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Restauración de backups ya guardados. Muestra una pantalla de confirmación (nombre exacto de
    /// la base de datos + contraseña actual) antes de ejecutar la restauración real, delegada en
    /// <see cref="BackupRestoreService"/>.
    /// </summary>
    [Authorize]
    public class RestoreController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly BackupRestoreService _restoreService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RestoreController> _logger;

        public RestoreController(ApplicationDbContext context, BackupRestoreService restoreService, UserManager<IdentityUser> userManager, ILogger<RestoreController> logger)
        {
            _context = context;
            _restoreService = restoreService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Pantalla de confirmación para restaurar un backup exitoso.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Confirm(int id)
        {
            var backup = await _context.BackupHistories.FindAsync(id);
            if (backup == null || backup.Status != "Exitoso")
            {
                TempData["Error"] = "El backup no existe o no se puede restaurar (solo se pueden restaurar backups exitosos).";
                return RedirectToAction("Index", "BackupHistory");
            }

            var model = new RestoreConfirmViewModel
            {
                BackupHistoryId = backup.Id,
                DatabaseName = backup.DatabaseName,
                DatabaseType = backup.DatabaseType ?? "Desconocido (backup antiguo)",
                StorageType = backup.StorageType ?? "Desconocido (backup antiguo)",
                BackupDate = backup.Date
            };

            return View(model);
        }

        /// <summary>
        /// Ejecuta la restauración tras validar la confirmación (nombre exacto + contraseña actual).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(RestoreConfirmViewModel model)
        {
            var backup = await _context.BackupHistories.FindAsync(model.BackupHistoryId);
            if (backup == null || backup.Status != "Exitoso")
            {
                TempData["Error"] = "El backup no existe o no se puede restaurar (solo se pueden restaurar backups exitosos).";
                return RedirectToAction("Index", "BackupHistory");
            }

            // Los datos informativos siempre vienen del backup real, nunca del formulario (no hay
            // razón para confiar en lo que mandó el cliente para esto).
            model.DatabaseName = backup.DatabaseName;
            model.DatabaseType = backup.DatabaseType ?? "Desconocido (backup antiguo)";
            model.StorageType = backup.StorageType ?? "Desconocido (backup antiguo)";
            model.BackupDate = backup.Date;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Confirmación explícita: el nombre debe coincidir exactamente (sin espacios de más al
            // copiar/pegar) con el de la base de datos que se va a sobrescribir.
            if (!string.Equals(model.ConfirmDatabaseName.Trim(), backup.DatabaseName, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(model.ConfirmDatabaseName), "El nombre no coincide exactamente con el de la base de datos.");
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !await _userManager.CheckPasswordAsync(currentUser, model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Contraseña incorrecta.");
                return View(model);
            }

            var (success, message) = await _restoreService.RestoreAsync(backup.Id, currentUser.UserName);

            if (success)
            {
                _logger.LogWarning("Restauración exitosa del backup {BackupHistoryId} ('{DatabaseName}') confirmada por {User}",
                    backup.Id, backup.DatabaseName, currentUser.UserName);
                TempData["Success"] = message;
            }
            else
            {
                _logger.LogError("Restauración fallida del backup {BackupHistoryId} ('{DatabaseName}') solicitada por {User}: {Message}",
                    backup.Id, backup.DatabaseName, currentUser.UserName, message);
                TempData["Error"] = message;
            }

            return RedirectToAction("Index", "BackupHistory");
        }

        /// <summary>
        /// Historial de auditoría de restauraciones (nunca se borra automáticamente).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> History()
        {
            var restores = await _context.RestoreHistories
                .OrderByDescending(r => r.Date)
                .ToListAsync();

            return View(restores);
        }
    }
}
