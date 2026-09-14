using BackupPro.Data;
using BackupPro.Models;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Ejecuta un backup de principio a fin: genera el backup con el <see cref="IDatabaseBackupProvider"/>
    /// del tipo de base de datos de la tarea y lo sube con el <see cref="IStorageProvider"/> del tipo
    /// de almacenamiento de la tarea. Usado tanto por la ejecución manual (botón "Ejecutar" en
    /// TaskSchedulerController) como por <see cref="BackupSchedulerBackgroundService"/>.
    ///
    /// Reemplaza lo que antes eran 20 métodos casi idénticos (uno por cada combinación de base de
    /// datos y almacenamiento) más una cadena larga de if/else para elegir cuál llamar.
    /// </summary>
    public class BackupExecutionService
    {
        private readonly BackupProviderRegistry _registry;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BackupExecutionService> _logger;

        public BackupExecutionService(BackupProviderRegistry registry, ApplicationDbContext context, ILogger<BackupExecutionService> logger)
        {
            _registry = registry;
            _context = context;
            _logger = logger;
        }

        public async Task<string> ExecuteAsync(Models.TaskScheduler task)
        {
            var databaseProvider = _registry.ResolveDatabase(task.DatabaseType);
            var storageProvider = _registry.ResolveStorage(task.StorageType);

            if (databaseProvider == null || storageProvider == null)
            {
                return $"Combinación no soportada o aún no implementada: {task.DatabaseType} a {task.StorageType}";
            }

            MemoryStream? backupStream = null;
            var startTime = DateTime.Now;

            try
            {
                var (backupSuccess, stream, fileName, databaseName, backupError) = await databaseProvider.CreateBackupAsync(task.DatabaseId);

                if (!backupSuccess || stream == null)
                {
                    // A diferencia de los IStorageProvider, los IDatabaseBackupProvider no registran
                    // el error en el histórico ellos mismos (todavía no hay a qué destino asociarlo),
                    // así que se registra acá.
                    await LogBackupErrorAsync(task.DatabaseId, databaseName, startTime, backupError);
                    throw new InvalidOperationException(backupError);
                }

                backupStream = stream;

                var (saveSuccess, _, fileSize, saveError) = await storageProvider.SaveBackupAsync(
                    task.StorageId, backupStream, fileName, databaseName, task.DatabaseId);

                if (!saveSuccess)
                {
                    // El provider de almacenamiento ya registró el error en el histórico.
                    throw new InvalidOperationException(saveError);
                }

                return $"Backup creado exitosamente: {fileName} ({StorageProviderBase.FormatBytes(fileSize)})";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar backup de la tarea {TaskName} ({DatabaseType} -> {StorageType})",
                    task.TaskName, task.DatabaseType, task.StorageType);
                throw new Exception($"Error al ejecutar backup: {ex.Message}", ex);
            }
            finally
            {
                backupStream?.Dispose();
            }
        }

        private async Task LogBackupErrorAsync(int databaseId, string databaseName, DateTime startTime, string errorMessage)
        {
            try
            {
                _context.BackupHistories.Add(new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Error",
                    Message = errorMessage,
                    BackupPath = "N/A"
                });

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Ignorar errores al registrar en histórico
            }
        }
    }
}
