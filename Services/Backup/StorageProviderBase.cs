using BackupPro.Data;
using BackupPro.Models;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Funcionalidad común a todos los <see cref="IStorageProvider"/>: registrar errores en el
    /// histórico de backups y formatear tamaños en bytes. Antes estaba duplicada de forma idéntica
    /// en cada uno de los 5 controladores de storage.
    /// </summary>
    public abstract class StorageProviderBase
    {
        protected readonly ApplicationDbContext Context;

        protected StorageProviderBase(ApplicationDbContext context)
        {
            Context = context;
        }

        protected async Task LogBackupErrorAsync(int databaseId, string databaseName, DateTime startTime, string errorMessage)
        {
            try
            {
                Context.BackupHistories.Add(new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Error",
                    Message = errorMessage,
                    BackupPath = "N/A"
                });

                await Context.SaveChangesAsync();
            }
            catch
            {
                // Ignorar errores al registrar en histórico
            }
        }

        protected async Task LogBackupSuccessAsync(int databaseId, string databaseName, DateTime startTime, string message, string backupPath,
            string storageType, int storageId, string? storageFileId = null)
        {
            try
            {
                Context.BackupHistories.Add(new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Exitoso",
                    Message = message,
                    BackupPath = backupPath,
                    StorageType = storageType,
                    StorageId = storageId,
                    StorageFileId = storageFileId
                });

                await Context.SaveChangesAsync();
            }
            catch
            {
                // Ignorar errores al registrar en histórico
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
