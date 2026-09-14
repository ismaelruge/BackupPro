using System.IO.Compression;
using BackupPro.Data;
using BackupPro.Services;
using FluentFTP;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Sube un backup a un servidor FTP usando una configuración guardada.</summary>
    public class FtpStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<FtpStorageProvider> _logger;

        public string StorageType => "Ftp";

        public FtpStorageProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<FtpStorageProvider> logger) : base(context)
        {
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string remoteFilePath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var ftpStorage = await Context.FtpStorages.FindAsync(storageId);
                if (ftpStorage == null)
                {
                    var errorMsg = "Configuración de almacenamiento FTP no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");
                remoteFilePath = $"{ftpStorage.RemotePath.TrimEnd('/')}/{zipFileName}";
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

                string plainPassword = _credentialProtector.Unprotect(ftpStorage.Password) ?? string.Empty;
                var client = new AsyncFtpClient(ftpStorage.Host, ftpStorage.Username, plainPassword, ftpStorage.Port);

                try
                {
                    await client.Connect();

                    var uploadResult = await client.UploadFile(localTempPath, remoteFilePath, FtpRemoteExists.Overwrite, true);

                    if (uploadResult != FtpStatus.Success)
                    {
                        var errorMsg = $"Error al subir archivo al FTP: {uploadResult}";
                        await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                        return (false, string.Empty, 0, errorMsg);
                    }

                    var remoteFileSize = await client.GetFileSize(remoteFilePath);
                    await client.Disconnect();

                    var duration = DateTime.Now - startTime;
                    await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                        $"Backup guardado exitosamente en FTP. Tamaño: {FormatBytes(remoteFileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                        remoteFilePath);

                    TryDeleteFile(localTempPath);

                    return (true, remoteFilePath, remoteFileSize, string.Empty);
                }
                finally
                {
                    client?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar backup en FTP {FtpStorageId}", storageId);
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en FTP: {ex.Message}";
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
    }
}
