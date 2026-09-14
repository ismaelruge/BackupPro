using BackupPro.Data;
using BackupPro.Models;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Restaura un backup ya guardado: lo descarga del destino de almacenamiento con el
    /// <see cref="IStorageProvider"/> correspondiente y lo restaura sobre la base de datos con el
    /// <see cref="IDatabaseBackupProvider"/> del motor correspondiente. Operación destructiva —
    /// reemplaza los datos actuales de la base de datos por los del backup — usada desde
    /// <see cref="Controllers.RestoreController"/> tras confirmar la contraseña del usuario y el
    /// nombre exacto de la base de datos.
    ///
    /// Cada intento (exitoso o no) queda registrado en <see cref="RestoreHistory"/>, que a
    /// diferencia de <see cref="BackupHistory"/> nunca se borra automáticamente.
    /// </summary>
    public class BackupRestoreService
    {
        private readonly BackupProviderRegistry _registry;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BackupRestoreService> _logger;

        public BackupRestoreService(BackupProviderRegistry registry, ApplicationDbContext context, ILogger<BackupRestoreService> logger)
        {
            _registry = registry;
            _context = context;
            _logger = logger;
        }

        public async Task<(bool success, string message)> RestoreAsync(int backupHistoryId, string? restoredByUserName)
        {
            var backup = await _context.BackupHistories.FindAsync(backupHistoryId);
            if (backup == null)
            {
                return (false, "El backup no existe.");
            }

            if (backup.Status != "Exitoso")
            {
                return (false, "Solo se pueden restaurar backups exitosos.");
            }

            if (string.IsNullOrEmpty(backup.DatabaseType) || string.IsNullOrEmpty(backup.StorageType) || backup.StorageId == null)
            {
                // Backups creados antes de que existieran estas columnas: no hay forma confiable de
                // saber con qué motor ni desde qué destino restaurar.
                const string legacyMessage = "Este backup no tiene registrado el motor de base de datos y/o el destino de almacenamiento (fue creado antes de que existiera la restauración automática); no se puede restaurar desde aquí.";
                await LogRestoreAsync(backup, restoredByUserName, success: false, legacyMessage);
                return (false, legacyMessage);
            }

            var databaseProvider = _registry.ResolveDatabase(backup.DatabaseType);
            var storageProvider = _registry.ResolveStorage(backup.StorageType);

            if (databaseProvider == null || storageProvider == null)
            {
                string message = $"Combinación no soportada o aún no implementada: {backup.DatabaseType} desde {backup.StorageType}";
                await LogRestoreAsync(backup, restoredByUserName, success: false, message);
                return (false, message);
            }

            MemoryStream? zipStream = null;

            try
            {
                var (downloadSuccess, stream, downloadError) = await storageProvider.DownloadBackupAsync(
                    backup.StorageId.Value, backup.BackupPath, backup.StorageFileId);

                if (!downloadSuccess || stream == null)
                {
                    await LogRestoreAsync(backup, restoredByUserName, success: false, downloadError);
                    return (false, downloadError);
                }

                zipStream = stream;

                var (restoreSuccess, restoreMessage) = await databaseProvider.RestoreBackupAsync(backup.DatabaseSourceId, zipStream);
                await LogRestoreAsync(backup, restoredByUserName, restoreSuccess, restoreMessage);
                return (restoreSuccess, restoreMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restaurar el backup {BackupHistoryId} ('{DatabaseName}')", backup.Id, backup.DatabaseName);
                string message = $"Error al restaurar: {ex.Message}";
                await LogRestoreAsync(backup, restoredByUserName, success: false, message);
                return (false, message);
            }
            finally
            {
                zipStream?.Dispose();
            }
        }

        private async Task LogRestoreAsync(BackupHistory backup, string? restoredByUserName, bool success, string message)
        {
            try
            {
                _context.RestoreHistories.Add(new RestoreHistory
                {
                    BackupHistoryId = backup.Id,
                    DatabaseName = backup.DatabaseName,
                    Date = DateTime.Now,
                    Status = success ? "Exitoso" : "Error",
                    Message = message,
                    RestoredBy = restoredByUserName
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo registrar el intento de restauración del backup {BackupHistoryId} en el histórico.", backup.Id);
            }
        }
    }
}
