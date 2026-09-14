using System.IO.Compression;
using System.Text;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services.OAuth;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Sube un backup a Google Drive usando una configuración guardada.</summary>
    public class GoogleDriveStorageProvider : StorageProviderBase, IStorageProvider
    {
        private readonly GoogleDriveTokenService _tokenService;
        private readonly ILogger<GoogleDriveStorageProvider> _logger;

        public string StorageType => "GoogleDrive";

        public GoogleDriveStorageProvider(ApplicationDbContext context, GoogleDriveTokenService tokenService, ILogger<GoogleDriveStorageProvider> logger)
            : base(context)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string googleDrivePath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                var googleDriveStorage = await Context.GoogleDriveStorages.FindAsync(storageId);
                if (googleDriveStorage == null)
                {
                    var errorMsg = "Configuración de Google Drive no encontrada";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                if (!await _tokenService.EnsureValidTokenAsync(googleDriveStorage))
                {
                    var errorMsg = "No hay un token de acceso válido para Google Drive. Por favor, autentícate primero.";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                string zipFileName = Path.ChangeExtension(fileName, ".zip");

                googleDrivePath = string.IsNullOrEmpty(googleDriveStorage.FolderPath)
                    ? zipFileName
                    : $"{googleDriveStorage.FolderPath.TrimEnd('/')}/{zipFileName}";

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

                string accessToken = _tokenService.GetPlainAccessToken(googleDriveStorage);
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var parentId = string.IsNullOrEmpty(googleDriveStorage.FolderId) ? "root" : googleDriveStorage.FolderId;
                var metadata = new { name = zipFileName, parents = new[] { parentId } };
                var metadataJson = System.Text.Json.JsonSerializer.Serialize(metadata);

                byte[] fileBytes = await File.ReadAllBytesAsync(localTempPath);

                var boundary = "===============" + DateTime.Now.Ticks.ToString("x") + "==";
                var delimiter = "\r\n--" + boundary + "\r\n";
                var closeDelimiter = "\r\n--" + boundary + "--";

                var multipartBody = new StringBuilder();
                multipartBody.Append(delimiter);
                multipartBody.Append("Content-Type: application/json; charset=UTF-8\r\n\r\n");
                multipartBody.Append(metadataJson);
                multipartBody.Append(delimiter);
                multipartBody.Append("Content-Type: application/zip\r\n");
                multipartBody.Append("Content-Transfer-Encoding: base64\r\n\r\n");
                multipartBody.Append(Convert.ToBase64String(fileBytes));
                multipartBody.Append(closeDelimiter);

                var content = new StringContent(multipartBody.ToString(), Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/related");
                content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("boundary", boundary));

                var response = await httpClient.PostAsync("https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,size", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var errorMsg = $"Error al subir archivo a Google Drive: {responseJson}";
                    await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                var responseData = System.Text.Json.JsonDocument.Parse(responseJson);

                long fileSize = responseData.RootElement.TryGetProperty("size", out var sizeElement)
                    ? long.Parse(sizeElement.GetString()!)
                    : new FileInfo(localTempPath).Length;

                string? uploadedFileId = responseData.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString()
                    : null;

                var duration = DateTime.Now - startTime;
                await LogBackupSuccessAsync(databaseId, databaseName, startTime,
                    $"Backup guardado exitosamente en Google Drive. Tamaño: {FormatBytes(fileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                    googleDrivePath, StorageType, storageId, uploadedFileId);

                TryDeleteFile(localTempPath);

                return (true, googleDrivePath, fileSize, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteFile(localTempPath);

                var errorMessage = $"Error al guardar backup en Google Drive: {ex.Message}";
                await LogBackupErrorAsync(databaseId, databaseName, startTime, errorMessage);
                _logger.LogError(ex, "Error al guardar backup en Google Drive {GoogleDriveStorageId}", storageId);
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
            if (string.IsNullOrEmpty(storageFileId))
            {
                // Backups guardados antes de que se empezara a registrar el id real del archivo de
                // Drive: no hay forma confiable de ubicarlo solo con la ruta, así que se deja el
                // registro tal cual (no se borra el archivo ni el histórico).
                _logger.LogWarning("No se puede borrar el backup de Google Drive {BackupPath}: no tiene un id de archivo registrado.", backupPath);
                return false;
            }

            var googleDriveStorage = await Context.GoogleDriveStorages.FindAsync(storageId);
            if (googleDriveStorage == null || !await _tokenService.EnsureValidTokenAsync(googleDriveStorage))
            {
                return false;
            }

            try
            {
                string accessToken = _tokenService.GetPlainAccessToken(googleDriveStorage);
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var response = await httpClient.DeleteAsync($"https://www.googleapis.com/drive/v3/files/{storageFileId}");

                // 404 significa que ya no existe (por ejemplo, si alguien lo borró manualmente desde
                // Drive): para efectos de retención, el resultado deseado (que no exista) ya se dio.
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al borrar backup en Google Drive {StorageFileId} (storage {GoogleDriveStorageId})", storageFileId, storageId);
                return false;
            }
        }
    }
}
