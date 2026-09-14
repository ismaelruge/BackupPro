using BackupPro.Data;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Aplica la política de retención "3x6": conserva siempre un mínimo de 3 backups exitosos de
    /// menos de 6 meses por cada combinación de base de datos + destino de almacenamiento; solo si
    /// ese mínimo está cubierto, borra los backups de esa misma combinación que ya superen los 6
    /// meses (archivo real en el destino, no solo el registro del histórico).
    ///
    /// Si hay backups de más de 6 meses pero todavía no existen 3 backups recientes (de menos de 6
    /// meses), no se borra nada: es el caso, por ejemplo, de una tarea que corre cada varios meses y
    /// todavía no acumuló suficiente historial reciente como para prescindir de los backups viejos.
    /// </summary>
    public class BackupRetentionService
    {
        private static readonly TimeSpan RetentionAge = TimeSpan.FromDays(30 * 6); // ~6 meses
        private const int MinimumRecentBackups = 3;

        private readonly ApplicationDbContext _context;
        private readonly BackupProviderRegistry _registry;
        private readonly ILogger<BackupRetentionService> _logger;

        public BackupRetentionService(ApplicationDbContext context, BackupProviderRegistry registry, ILogger<BackupRetentionService> logger)
        {
            _context = context;
            _registry = registry;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTime.Now - RetentionAge;

            // Solo backups exitosos con storage conocido: los de error nunca tuvieron archivo, y los
            // creados antes de que existiera esta columna no tienen forma confiable de saber dónde
            // borrar el archivo real, así que se dejan intactos.
            var successfulBackups = await _context.BackupHistories
                .Where(h => h.Status == "Exitoso" && h.StorageType != null && h.StorageId != null)
                .ToListAsync(cancellationToken);

            var series = successfulBackups.GroupBy(h => (h.DatabaseSourceId, h.DatabaseName, h.StorageType, h.StorageId));

            foreach (var group in series)
            {
                var old = group.Where(h => h.Date < cutoff).ToList();
                if (old.Count == 0)
                {
                    continue;
                }

                int recentCount = group.Count(h => h.Date >= cutoff);
                if (recentCount < MinimumRecentBackups)
                {
                    _logger.LogInformation(
                        "Retención: se omite limpieza de '{DatabaseName}' ({StorageType}/{StorageId}): solo hay {Recent} backup(s) de menos de 6 meses (mínimo {Minimum}).",
                        group.Key.DatabaseName, group.Key.StorageType, group.Key.StorageId, recentCount, MinimumRecentBackups);
                    continue;
                }

                foreach (var backup in old)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await DeleteExpiredBackupAsync(backup, cancellationToken);
                }
            }
        }

        private async Task DeleteExpiredBackupAsync(Models.BackupHistory backup, CancellationToken cancellationToken)
        {
            try
            {
                var provider = _registry.ResolveStorage(backup.StorageType!);
                if (provider == null)
                {
                    _logger.LogWarning("Retención: no se encontró un provider de almacenamiento '{StorageType}' para borrar el backup {Id}.", backup.StorageType, backup.Id);
                    return;
                }

                bool deleted = await provider.DeleteBackupAsync(backup.StorageId!.Value, backup.BackupPath, backup.StorageFileId);
                if (!deleted)
                {
                    _logger.LogWarning("Retención: no se pudo borrar el archivo del backup {Id} ('{DatabaseName}', {Date}); se conserva para reintentar más adelante.",
                        backup.Id, backup.DatabaseName, backup.Date);
                    return;
                }

                _context.BackupHistories.Remove(backup);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Retención: backup {Id} ('{DatabaseName}', {Date}) borrado por política de retención (más de 6 meses).",
                    backup.Id, backup.DatabaseName, backup.Date);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retención: error al borrar el backup {Id} ('{DatabaseName}').", backup.Id, backup.DatabaseName);
            }
        }
    }
}
