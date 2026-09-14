using System.IO.Compression;
using Azure.Storage.Blobs;
using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Sube un backup a Azure Blob Storage usando una configuración guardada.</summary>
    public class BlobStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<BlobStorageProvider> _logger;

        public string StorageType => "AzureBlob";

        public BlobStorageProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<BlobStorageProvider> logger) : base(context)
        {
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string blobPath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var blobStorage = await Context.AzureBlobStorages.FindAsync(storageId);
                if (blobStorage == null)
                {
                    var errorMsg = "Configuración de Azure Blob Storage no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");

                blobPath = string.IsNullOrEmpty(blobStorage.BlobPrefix)
                    ? zipFileName
                    : $"{blobStorage.BlobPrefix.TrimEnd('/')}/{zipFileName}";

                localTempPath = Path.Combine(Path.GetTempPath(), zipFileName);

                using (var fileStream = new FileStream(localTempPath, FileMode.Create, FileAccess.Write))
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    {
                        backupStream.Position = 0;
                        await backupStream.CopyToAsync(entryStream);
                    }
                }

                string plainConnectionString = _credentialProtector.Unprotect(blobStorage.ConnectionString) ?? string.Empty;
                var blobServiceClient = new BlobServiceClient(plainConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(blobStorage.ContainerName);

                await containerClient.CreateIfNotExistsAsync();
                var blobClient = containerClient.GetBlobClient(blobPath);

                using (var uploadStream = File.OpenRead(localTempPath))
                {
                    await blobClient.UploadAsync(uploadStream, overwrite: true);
                }

                var properties = await blobClient.GetPropertiesAsync();
                long blobSize = properties.Value.ContentLength;

                var duration = DateTime.Now - startTime;
                await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                    $"Backup guardado exitosamente en Azure Blob Storage. Tamaño: {FormatBytes(blobSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                    blobPath, StorageType, storageId);

                TryDeleteFile(localTempPath);

                return (true, blobPath, blobSize, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar backup en Azure Blob Storage {BlobStorageId}", storageId);
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en Azure Blob Storage: {ex.Message}";
                await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMessage);
                return (false, string.Empty, 0, errorMessage);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* Ignorar errores al eliminar archivo temporal */ }
        }

        public async Task<bool> DeleteBackupAsync(int storageId, string backupPath, string? storageFileId)
        {
            var blobStorage = await Context.AzureBlobStorages.FindAsync(storageId);
            if (blobStorage == null)
            {
                return false;
            }

            try
            {
                string plainConnectionString = _credentialProtector.Unprotect(blobStorage.ConnectionString) ?? string.Empty;
                var blobServiceClient = new BlobServiceClient(plainConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(blobStorage.ContainerName);
                var blobClient = containerClient.GetBlobClient(backupPath);

                await blobClient.DeleteIfExistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar blob {BackupPath} (storage {BlobStorageId})", backupPath, storageId);
                return false;
            }
        }
    }
}
