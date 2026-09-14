using System.IO.Compression;
using BackupPro.Data;
using BackupPro.Services.OAuth;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Sube un backup a OneDrive usando una configuración guardada.</summary>
    public class OneDriveStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly OneDriveTokenService _tokenService;
        private readonly ILogger<OneDriveStorageProvider> _logger;

        public string StorageType => "OneDrive";

        public OneDriveStorageProvider(ApplicationDbContext context, OneDriveTokenService tokenService, ILogger<OneDriveStorageProvider> logger)
            : base(context)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string oneDrivePath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var oneDriveStorage = await Context.OneDriveStorages.FindAsync(storageId);
                if (oneDriveStorage == null)
                {
                    var errorMsg = "Configuración de OneDrive no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                if (!await _tokenService.EnsureValidTokenAsync(oneDriveStorage))
                {
                    var errorMsg = "No hay un token de acceso válido para OneDrive. Por favor, autentícate primero.";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");

                oneDrivePath = string.IsNullOrEmpty(oneDriveStorage.FolderPath)
                    ? zipFileName
                    : $"{oneDriveStorage.FolderPath.TrimEnd('/')}/{zipFileName}";

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

                string accessToken = _tokenService.GetPlainAccessToken(oneDriveStorage);
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                string uploadUrl = string.IsNullOrEmpty(oneDriveStorage.ItemId)
                    ? $"https://graph.microsoft.com/v1.0/me/drive/root:/{zipFileName}:/content"
                    : $"https://graph.microsoft.com/v1.0/me/drive/items/{oneDriveStorage.ItemId}:/{zipFileName}:/content";

                using (var uploadStream = File.OpenRead(localTempPath))
                {
                    var content = new StreamContent(uploadStream);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    var response = await httpClient.PutAsync(uploadUrl, content);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = $"Error al subir archivo a OneDrive: {responseJson}";
                        await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                        return (false, string.Empty, 0, errorMsg);
                    }

                    var responseData = System.Text.Json.JsonDocument.Parse(responseJson);
                    long fileSize = responseData.RootElement.GetProperty("size").GetInt64();

                    var duration = DateTime.Now - startTime;
                    await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                        $"Backup guardado exitosamente en OneDrive. Tamaño: {FormatBytes(fileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                        oneDrivePath);

                    TryDeleteFile(localTempPath);

                    return (true, oneDrivePath, fileSize, string.Empty);
                }
            }
            catch (Exception ex)
            {
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en OneDrive: {ex.Message}";
                await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMessage);
                _logger.LogError(ex, "Error al guardar backup en OneDrive {OneDriveStorageId}", storageId);
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
