using System.IO.Compression;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Sube un backup a Amazon S3 usando una configuración guardada.</summary>
    public class S3StorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<S3StorageProvider> _logger;

        public string StorageType => "S3";

        public S3StorageProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<S3StorageProvider> logger) : base(context)
        {
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId, string databaseType)
        {
            var startTime = DateTime.Now;
            string s3Key = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var s3Storage = await Context.S3Storages.FindAsync(storageId);
                if (s3Storage == null)
                {
                    var errorMsg = "Configuración de Amazon S3 no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");

                s3Key = string.IsNullOrEmpty(s3Storage.Prefix)
                    ? zipFileName
                    : $"{s3Storage.Prefix.TrimEnd('/')}/{zipFileName}";

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

                string plainSecretAccessKey = _credentialProtector.Unprotect(s3Storage.SecretAccessKey) ?? string.Empty;
                var credentials = new BasicAWSCredentials(s3Storage.AccessKeyId, plainSecretAccessKey);
                using var s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(s3Storage.Region));

                using (var uploadStream = File.OpenRead(localTempPath))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = s3Storage.BucketName,
                        Key = s3Key,
                        InputStream = uploadStream
                    };

                    await s3Client.PutObjectAsync(putRequest);
                }

                var fileInfo = new FileInfo(localTempPath);
                long fileSize = fileInfo.Length;

                var duration = DateTime.Now - startTime;
                await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                    $"Backup guardado exitosamente en Amazon S3. Tamaño: {FormatBytes(fileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                    s3Key, StorageType, storageId, databaseType);

                TryDeleteFile(localTempPath);

                return (true, s3Key, fileSize, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar backup en Amazon S3 {S3StorageId}", storageId);
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en Amazon S3: {ex.Message}";
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
            var s3Storage = await Context.S3Storages.FindAsync(storageId);
            if (s3Storage == null)
            {
                return false;
            }

            try
            {
                string plainSecretAccessKey = _credentialProtector.Unprotect(s3Storage.SecretAccessKey) ?? string.Empty;
                var credentials = new BasicAWSCredentials(s3Storage.AccessKeyId, plainSecretAccessKey);
                using var s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(s3Storage.Region));

                await s3Client.DeleteObjectAsync(s3Storage.BucketName, backupPath);

                // Un delete de una clave que ya no existe en S3 también responde como éxito (S3 es
                // idempotente al borrar): para efectos de retención, el resultado deseado (que no
                // exista) ya se dio, igual que con el manejo de 404 en Google Drive/OneDrive.
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar backup en Amazon S3 {BackupPath} (storage {S3StorageId})", backupPath, storageId);
                return false;
            }
        }

        public async Task<(bool success, MemoryStream? stream, string errorMessage)> DownloadBackupAsync(int storageId, string backupPath, string? storageFileId)
        {
            var s3Storage = await Context.S3Storages.FindAsync(storageId);
            if (s3Storage == null)
            {
                return (false, null, "Configuración de Amazon S3 no encontrada");
            }

            try
            {
                string plainSecretAccessKey = _credentialProtector.Unprotect(s3Storage.SecretAccessKey) ?? string.Empty;
                var credentials = new BasicAWSCredentials(s3Storage.AccessKeyId, plainSecretAccessKey);
                using var s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(s3Storage.Region));

                using var response = await s3Client.GetObjectAsync(s3Storage.BucketName, backupPath);

                var stream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(stream);
                stream.Position = 0;
                return (true, stream, string.Empty);
            }
            catch (AmazonS3Exception ex)
            {
                _logger.LogError(ex, "Error al descargar backup de Amazon S3 {BackupPath} (storage {S3StorageId})", backupPath, storageId);
                return (false, null, $"Error al descargar de Amazon S3: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar backup de Amazon S3 {BackupPath} (storage {S3StorageId})", backupPath, storageId);
                return (false, null, $"Error al descargar de Amazon S3: {ex.Message}");
            }
        }
    }
}
