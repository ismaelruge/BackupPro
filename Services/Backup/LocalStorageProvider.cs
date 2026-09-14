using System.IO.Compression;
using BackupPro.Data;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Guarda un backup en una carpeta local del servidor.</summary>
    public class LocalStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly ILogger<LocalStorageProvider> _logger;

        public string StorageType => "Local";

        public LocalStorageProvider(ApplicationDbContext context, ILogger<LocalStorageProvider> logger) : base(context)
        {
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string backupFilePath = string.Empty;

            try
            {
                var localStorage = await Context.LocalStorages.FindAsync(storageId);
                if (localStorage == null)
                {
                    var errorMsg = "Configuración de almacenamiento local no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                if (!Directory.Exists(localStorage.FolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(localStorage.FolderPath);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"No se pudo crear la carpeta de destino: {ex.Message}";
                        await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                        return (false, string.Empty, 0, errorMsg);
                    }
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");
                backupFilePath = Path.Combine(localStorage.FolderPath, zipFileName);

                using (var fileStream = new FileStream(backupFilePath, FileMode.Create, FileAccess.Write))
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    {
                        backupStream.Position = 0;
                        await backupStream.CopyToAsync(entryStream);
                    }
                }

                if (!File.Exists(backupFilePath))
                {
                    var errorMsg = "El archivo de backup no se creó correctamente";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                var fileInfo = new FileInfo(backupFilePath);
                var duration = DateTime.Now - startTime;

                await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                    $"Backup guardado exitosamente. Tamaño: {FormatBytes(fileInfo.Length)}. Duración: {duration.TotalSeconds:F2} segundos.",
                    backupFilePath);

                return (true, backupFilePath, fileInfo.Length, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar backup en almacenamiento local {LocalStorageId}", storageId);
                var errorMessage = $"Error al guardar backup: {ex.Message}";

                if (!string.IsNullOrEmpty(backupFilePath) && File.Exists(backupFilePath))
                {
                    try
                    {
                        File.Delete(backupFilePath);
                    }
                    catch { /* Ignorar errores al eliminar */ }
                }

                await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMessage);
                return (false, string.Empty, 0, errorMessage);
            }
        }
    }
}
