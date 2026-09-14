using System.IO.Compression;
using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;
using Renci.SshNet;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Sube un backup a un servidor SFTP (SSH File Transfer Protocol) usando una configuración
    /// guardada. A diferencia de <see cref="FtpStorageProvider"/>, el transporte va cifrado por SSH
    /// desde la primera conexión (no depende de una extensión TLS opcional como FTPS).
    ///
    /// SSH.NET (Renci.SshNet) expone una API síncrona; las llamadas bloqueantes se envuelven en
    /// <see cref="Task.Run(Action)"/> para mantener honesta la firma async de esta clase, igual que
    /// hacen otros providers de esta carpeta con SDKs síncronos.
    /// </summary>
    public class SftpStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<SftpStorageProvider> _logger;

        public string StorageType => "Sftp";

        public SftpStorageProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<SftpStorageProvider> logger) : base(context)
        {
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId, string databaseType)
        {
            var startTime = DateTime.Now;
            string remoteFilePath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var sftpStorage = await Context.SftpStorages.FindAsync(storageId);
                if (sftpStorage == null)
                {
                    var errorMsg = "Configuración de almacenamiento SFTP no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");
                remoteFilePath = $"{sftpStorage.RemotePath.TrimEnd('/')}/{zipFileName}";
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

                string plainPassword = _credentialProtector.Unprotect(sftpStorage.Password) ?? string.Empty;
                using var client = new SftpClient(sftpStorage.Host, sftpStorage.Port, sftpStorage.Username, plainPassword);

                try
                {
                    await Task.Run(() => client.Connect());

                    using (var uploadStream = new FileStream(localTempPath, FileMode.Open, FileAccess.Read))
                    {
                        await Task.Run(() => client.UploadFile(uploadStream, remoteFilePath, true));
                    }

                    long remoteFileSize = new FileInfo(localTempPath).Length;

                    var duration = DateTime.Now - startTime;
                    await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                        $"Backup guardado exitosamente en SFTP. Tamaño: {FormatBytes(remoteFileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                        remoteFilePath, StorageType, storageId, databaseType);

                    TryDeleteFile(localTempPath);

                    return (true, remoteFilePath, remoteFileSize, string.Empty);
                }
                finally
                {
                    if (client.IsConnected)
                    {
                        client.Disconnect();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar backup en SFTP {SftpStorageId}", storageId);
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en SFTP: {ex.Message}";
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
            var sftpStorage = await Context.SftpStorages.FindAsync(storageId);
            if (sftpStorage == null)
            {
                return false;
            }

            SftpClient? client = null;
            try
            {
                string plainPassword = _credentialProtector.Unprotect(sftpStorage.Password) ?? string.Empty;
                client = new SftpClient(sftpStorage.Host, sftpStorage.Port, sftpStorage.Username, plainPassword);
                await Task.Run(() => client.Connect());

                if (client.Exists(backupPath))
                {
                    await Task.Run(() => client.DeleteFile(backupPath));
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar backup en SFTP {BackupPath} (storage {SftpStorageId})", backupPath, storageId);
                return false;
            }
            finally
            {
                if (client is { IsConnected: true })
                {
                    client.Disconnect();
                }

                client?.Dispose();
            }
        }

        public async Task<(bool success, MemoryStream? stream, string errorMessage)> DownloadBackupAsync(int storageId, string backupPath, string? storageFileId)
        {
            var sftpStorage = await Context.SftpStorages.FindAsync(storageId);
            if (sftpStorage == null)
            {
                return (false, null, "Configuración de almacenamiento SFTP no encontrada");
            }

            SftpClient? client = null;
            try
            {
                string plainPassword = _credentialProtector.Unprotect(sftpStorage.Password) ?? string.Empty;
                client = new SftpClient(sftpStorage.Host, sftpStorage.Port, sftpStorage.Username, plainPassword);
                await Task.Run(() => client.Connect());

                var stream = new MemoryStream();
                await Task.Run(() => client.DownloadFile(backupPath, stream));

                stream.Position = 0;
                return (true, stream, string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al descargar backup de SFTP {BackupPath} (storage {SftpStorageId})", backupPath, storageId);
                return (false, null, $"Error al descargar de SFTP: {ex.Message}");
            }
            finally
            {
                if (client is { IsConnected: true })
                {
                    client.Disconnect();
                }

                client?.Dispose();
            }
        }
    }
}
