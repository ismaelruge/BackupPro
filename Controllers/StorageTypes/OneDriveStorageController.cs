using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class OneDriveStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OneDriveSettings _oneDriveSettings;

        public OneDriveStorageController(ApplicationDbContext context, OneDriveSettings oneDriveSettings)
        {
            _context = context;
            _oneDriveSettings = oneDriveSettings;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.OneDriveStorages.ToListAsync();
            return View(list);
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de OneDrive.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] OneDriveStorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.OneDriveStorages.AnyAsync(o => o.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var oneDrive = new OneDriveStorage
                    {
                        ConfigurationName = model.ConfigurationName,
                        Email = model.Email,
                        ItemId = model.ItemId ?? string.Empty,
                        FolderPath = model.FolderPath ?? string.Empty,
                        AccessToken = null, // Se guardará después de la autenticación
                        RefreshToken = null,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.OneDriveStorages.Add(oneDrive);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de OneDrive creada exitosamente.", id = oneDrive.Id });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de OneDrive existente.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] OneDriveStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var oneDrive = await _context.OneDriveStorages.FindAsync(model.Id.Value);
                if (oneDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.OneDriveStorages.AnyAsync(o => o.ConfigurationName == model.ConfigurationName && o.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                oneDrive.ConfigurationName = model.ConfigurationName;
                oneDrive.Email = model.Email;
                oneDrive.ItemId = model.ItemId ?? string.Empty;
                oneDrive.FolderPath = model.FolderPath ?? string.Empty;
                oneDrive.LastModifiedAt = DateTime.Now;

                _context.OneDriveStorages.Update(oneDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de OneDrive.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(id);
                if (oneDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.OneDriveStorages.Remove(oneDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene una configuración de OneDrive por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(id);
                if (oneDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new OneDriveStorageViewModel
                {
                    Id = oneDrive.Id,
                    ConfigurationName = oneDrive.ConfigurationName,
                    Email = oneDrive.Email,
                    ItemId = oneDrive.ItemId,
                    FolderPath = oneDrive.FolderPath,
                    TokenExpiresAt = oneDrive.TokenExpiresAt
                };

                // Incluir AccessToken para permitir exploración sin reconectar
                // TODO: En producción, verificar expiración antes de enviar
                return Json(new {
                    success = true,
                    data = viewModel,
                    accessToken = oneDrive.AccessToken, // Para usar el explorador sin reconectar
                    hasValidToken = !string.IsNullOrEmpty(oneDrive.AccessToken)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Guarda los tokens de OneDrive para una configuración.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveTokens([FromBody] SaveTokensRequest request)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(request.Id);
                if (oneDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // TODO: En producción, encriptar los tokens antes de guardarlos
                oneDrive.AccessToken = request.AccessToken;
                oneDrive.RefreshToken = request.RefreshToken;
                oneDrive.TokenExpiresAt = DateTime.Now.AddHours(1); // Los tokens de Microsoft duran ~1 hora
                oneDrive.LastModifiedAt = DateTime.Now;

                _context.OneDriveStorages.Update(oneDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Tokens guardados exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al guardar tokens: {ex.Message}" });
            }
        }

        // ========== ONEDRIVE ENDPOINTS ==========

        [HttpGet]
        public IActionResult OneDriveAuth()
        {
            try
            {
                var clientId = _oneDriveSettings.ClientId;
                var redirectUri = _oneDriveSettings.RedirectUri;

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return BadRequest("OneDrive OAuth configuration is missing");
                }

                var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
                              $"client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString("Files.ReadWrite.All offline_access User.Read")}" +
                              $"&response_mode=query" +
                              $"&prompt=consent";

                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error during authentication: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> OneDriveCallback(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                var errorScript = @"
                    <html>
                    <head><title>Error de autenticación</title></head>
                    <body>
                        <script>
                            try {
                                if (window.opener && !window.opener.closed) {
                                    window.opener.postMessage({success: false, message: 'No se recibió el código de autorización'}, window.location.origin);
                                }
                            } catch (e) {
                                console.error('Error al enviar mensaje:', e);
                            }
                            setTimeout(function() { window.close(); }, 1000);
                        </script>
                        <p>Error de autenticación. Esta ventana se cerrará automáticamente...</p>
                    </body>
                    </html>";
                return Content(errorScript, "text/html");
            }

            try
            {
                var clientId = _oneDriveSettings.ClientId;
                var clientSecret = _oneDriveSettings.ClientSecret;
                var redirectUri = _oneDriveSettings.RedirectUri;

                // Intercambiar el código por tokens
                var tokenRequest = new Dictionary<string, string>
                {
                    {"code", code},
                    {"client_id", clientId!},
                    {"client_secret", clientSecret!},
                    {"redirect_uri", redirectUri!},
                    {"grant_type", "authorization_code"}
                };

                using var httpClient = new HttpClient();
                var tokenResponse = await httpClient.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token",
                    new FormUrlEncodedContent(tokenRequest));

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorScript = $@"
                        <html>
                        <head><title>Error de autenticación</title></head>
                        <body>
                            <script>
                                try {{
                                    if (window.opener && !window.opener.closed) {{
                                        window.opener.postMessage({{success: false, message: 'Error al obtener tokens'}}, window.location.origin);
                                    }}
                                }} catch (e) {{
                                    console.error('Error al enviar mensaje:', e);
                                }}
                                setTimeout(function() {{ window.close(); }}, 1000);
                            </script>
                            <p>Error al obtener tokens. Esta ventana se cerrará automáticamente...</p>
                        </body>
                        </html>";
                    return Content(errorScript, "text/html");
                }

                // Obtener información del usuario
                var tokenData = System.Text.Json.JsonDocument.Parse(tokenJson);
                var accessToken = tokenData.RootElement.GetProperty("access_token").GetString();

                // Intentar obtener el refresh token
                string? refreshToken = null;
                bool hasRefreshToken = false;
                if (tokenData.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
                {
                    refreshToken = refreshTokenElement.GetString();
                    hasRefreshToken = !string.IsNullOrEmpty(refreshToken);
                }

                // Obtener información del usuario de Microsoft Graph
                var userInfoResponse = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                userInfoResponse = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
                var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                var userInfo = System.Text.Json.JsonDocument.Parse(userInfoJson);
                var email = userInfo.RootElement.GetProperty("userPrincipalName").GetString();

                // Guardar tokens en sesión (temporal)
                try
                {
                    HttpContext.Session.SetString($"OneDrive_AccessToken_{email}", accessToken!);
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        HttpContext.Session.SetString($"OneDrive_RefreshToken_{email}", refreshToken);
                    }
                }
                catch (Exception sessionEx)
                {
                    Console.WriteLine($"No se pudo guardar en sesión: {sessionEx.Message}");
                }

                // Escapar valores para JavaScript
                var safeEmail = System.Text.Json.JsonSerializer.Serialize(email);
                var safeAccessToken = System.Text.Json.JsonSerializer.Serialize(accessToken);
                var safeRefreshToken = refreshToken != null ? System.Text.Json.JsonSerializer.Serialize(refreshToken) : "null";
                var hasRefreshTokenJson = hasRefreshToken.ToString().ToLower();

                // Retornar éxito con el email, accessToken y refreshToken
                var successScript = $@"
                    <html>
                    <head><title>Autenticación exitosa</title></head>
                    <body>
                        <script>
                            try {{
                                if (window.opener && !window.opener.closed) {{
                                    window.opener.postMessage({{
                                        success: true,
                                        email: {safeEmail},
                                        accessToken: {safeAccessToken},
                                        refreshToken: {safeRefreshToken},
                                        hasRefreshToken: {hasRefreshTokenJson}
                                    }}, window.location.origin);
                                }}
                            }} catch (e) {{
                                console.error('Error al enviar mensaje:', e);
                            }}
                            setTimeout(function() {{ window.close(); }}, 1000);
                        </script>
                        <p>Autenticación exitosa. Esta ventana se cerrará automáticamente...</p>
                    </body>
                    </html>";

                return Content(successScript, "text/html");
            }
            catch (Exception ex)
            {
                var errorMsg = System.Text.Json.JsonSerializer.Serialize(ex.Message).Replace("'", "\\'");
                var errorScript = $@"
                    <html>
                    <head><title>Error de autenticación</title></head>
                    <body>
                        <script>
                            try {{
                                if (window.opener && !window.opener.closed) {{
                                    window.opener.postMessage({{success: false, message: {errorMsg}}}, window.location.origin);
                                }}
                            }} catch (e) {{
                                console.error('Error al enviar mensaje:', e);
                            }}
                            setTimeout(function() {{ window.close(); }}, 1000);
                        </script>
                        <p>Error durante la autenticación. Esta ventana se cerrará automáticamente...</p>
                    </body>
                    </html>";
                return Content(errorScript, "text/html");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ListOneDriveFolders([FromBody] OneDriveListRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.AccessToken}");

                // Construir la URL base
                var url = string.IsNullOrEmpty(request.ItemId)
                    ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
                    : $"https://graph.microsoft.com/v1.0/me/drive/items/{request.ItemId}/children";

                // Filtrar solo carpetas
                url += "?$filter=folder ne null&$select=id,name,lastModifiedDateTime,folder&$orderby=name";

                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error al listar carpetas: {json}" });
                }

                var data = System.Text.Json.JsonDocument.Parse(json);
                var folders = new List<OneDriveFolderInfo>();

                if (data.RootElement.TryGetProperty("value", out var valuesElement))
                {
                    foreach (var item in valuesElement.EnumerateArray())
                    {
                        folders.Add(new OneDriveFolderInfo
                        {
                            Id = item.GetProperty("id").GetString()!,
                            Name = item.GetProperty("name").GetString()!,
                            LastModifiedDateTime = item.TryGetProperty("lastModifiedDateTime", out var modTime)
                                ? DateTime.Parse(modTime.GetString()!)
                                : DateTime.MinValue
                        });
                    }
                }

                return Json(new { success = true, folders = folders });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateOneDriveFolder([FromBody] OneDriveCreateFolderRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.AccessToken}");

                // Construir la URL base
                var url = string.IsNullOrEmpty(request.ParentId)
                    ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
                    : $"https://graph.microsoft.com/v1.0/me/drive/items/{request.ParentId}/children";

                // Crear el JSON manualmente para incluir @microsoft.graph.conflictBehavior
                var jsonBody = $@"{{
                    ""name"": ""{request.FolderName}"",
                    ""folder"": {{}},
                    ""@microsoft.graph.conflictBehavior"": ""fail""
                }}";

                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error al crear carpeta: {responseJson}" });
                }

                var data = System.Text.Json.JsonDocument.Parse(responseJson);
                var folderId = data.RootElement.GetProperty("id").GetString();

                return Json(new { success = true, folderId = folderId, message = "Carpeta creada exitosamente" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // ========== TOKEN REFRESH ==========

        /// <summary>
        /// Renueva el access token usando el refresh token
        /// </summary>
        private async Task<bool> RefreshAccessToken(OneDriveStorage oneDriveStorage)
        {
            try
            {
                if (string.IsNullOrEmpty(oneDriveStorage.RefreshToken))
                {
                    return false;
                }

                var tokenRequest = new Dictionary<string, string>
                {
                    {"refresh_token", oneDriveStorage.RefreshToken},
                    {"client_id", _oneDriveSettings.ClientId!},
                    {"client_secret", _oneDriveSettings.ClientSecret!},
                    {"grant_type", "refresh_token"}
                };

                using var httpClient = new HttpClient();
                var tokenResponse = await httpClient.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token",
                    new FormUrlEncodedContent(tokenRequest));

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
                var tokenData = System.Text.Json.JsonDocument.Parse(tokenJson);
                var newAccessToken = tokenData.RootElement.GetProperty("access_token").GetString();

                // Actualizar el token en la base de datos
                oneDriveStorage.AccessToken = newAccessToken;

                // Actualizar refresh token si viene uno nuevo
                if (tokenData.RootElement.TryGetProperty("refresh_token", out var newRefreshToken))
                {
                    oneDriveStorage.RefreshToken = newRefreshToken.GetString();
                }

                oneDriveStorage.TokenExpiresAt = DateTime.Now.AddHours(1);
                oneDriveStorage.LastModifiedAt = DateTime.Now;

                _context.OneDriveStorages.Update(oneDriveStorage);
                await _context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica si el token está expirado y lo renueva si es necesario
        /// </summary>
        private async Task<bool> EnsureValidToken(OneDriveStorage oneDriveStorage)
        {
            // Si no hay token, no se puede renovar
            if (string.IsNullOrEmpty(oneDriveStorage.AccessToken))
            {
                return false;
            }

            // Si el token no ha expirado, está válido
            if (oneDriveStorage.TokenExpiresAt.HasValue && oneDriveStorage.TokenExpiresAt.Value > DateTime.Now.AddMinutes(5))
            {
                return true;
            }

            // Token expirado o próximo a expirar, intentar renovar
            return await RefreshAccessToken(oneDriveStorage);
        }

        /// <summary>
        /// Registra un error en el histórico de backups
        /// </summary>
        private async Task LogBackupError(int databaseId, string databaseName, DateTime startTime, string errorMessage)
        {
            try
            {
                var backupHistory = new BackupHistory
                {
                    DatabaseSourceId = databaseId,
                    DatabaseName = databaseName,
                    Date = startTime,
                    Status = "Error",
                    Message = errorMessage,
                    BackupPath = "N/A"
                };

                _context.BackupHistories.Add(backupHistory);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Ignorar errores al registrar en histórico
            }
        }

        // ========== BACKUP OPERATIONS ==========

        /// <summary>
        /// Guarda un backup (MemoryStream) en OneDrive
        /// </summary>
        /// <param name="oneDriveStorageId">ID de la configuración de OneDrive</param>
        /// <param name="backupStream">Stream del backup</param>
        /// <param name="fileName">Nombre del archivo</param>
        /// <param name="databaseName">Nombre de la base de datos</param>
        /// <param name="databaseId">ID de la base de datos de origen</param>
        /// <returns>Tuple con el resultado de la operación</returns>
        public async Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackup(int oneDriveStorageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId)
        {
            var startTime = DateTime.Now;
            string oneDrivePath = string.Empty;
            string localTempPath = string.Empty;

            try
            {
                // Obtener configuración de OneDrive
                var oneDriveStorage = await _context.OneDriveStorages.FindAsync(oneDriveStorageId);
                if (oneDriveStorage == null)
                {
                    var errorMsg = "Configuración de OneDrive no encontrada";
                    await LogBackupError(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                // Verificar y renovar token si es necesario
                if (!await EnsureValidToken(oneDriveStorage))
                {
                    var errorMsg = "No hay un token de acceso válido para OneDrive. Por favor, autentícate primero.";
                    await LogBackupError(databaseId, databaseName, startTime, errorMsg);
                    return (false, string.Empty, 0, errorMsg);
                }

                // Cambiar extensión a .zip
                string zipFileName = Path.ChangeExtension(fileName, ".zip");

                // Construir ruta de OneDrive
                oneDrivePath = string.IsNullOrEmpty(oneDriveStorage.FolderPath)
                    ? zipFileName
                    : $"{oneDriveStorage.FolderPath.TrimEnd('/')}/{zipFileName}";

                // Crear archivo temporal local comprimido
                localTempPath = Path.Combine(Path.GetTempPath(), zipFileName);

                // Comprimir el backup en un archivo .zip temporal
                using (var fileStream = new FileStream(localTempPath, FileMode.Create, FileAccess.Write))
                using (var zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create, false))
                {
                    var entry = zipArchive.CreateEntry(fileName, CompressionLevel.Optimal);

                    using (var entryStream = entry.Open())
                    {
                        backupStream.Position = 0; // Asegurar que el stream está al inicio
                        await backupStream.CopyToAsync(entryStream);
                    }
                }

                // Subir a OneDrive usando Microsoft Graph API
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {oneDriveStorage.AccessToken}");

                // Determinar la URL de upload
                string uploadUrl;
                if (string.IsNullOrEmpty(oneDriveStorage.ItemId))
                {
                    // Subir a la raíz o a una ruta específica
                    uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{zipFileName}:/content";
                }
                else
                {
                    // Subir a una carpeta específica por ID
                    uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{oneDriveStorage.ItemId}:/{zipFileName}:/content";
                }

                // Leer el archivo y subirlo
                using (var uploadStream = System.IO.File.OpenRead(localTempPath))
                {
                    var content = new StreamContent(uploadStream);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");

                    var response = await httpClient.PutAsync(uploadUrl, content);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = $"Error al subir archivo a OneDrive: {responseJson}";
                        await LogBackupError(databaseId, databaseName, startTime, errorMsg);
                        return (false, string.Empty, 0, errorMsg);
                    }

                    // Obtener tamaño del archivo desde la respuesta
                    var responseData = System.Text.Json.JsonDocument.Parse(responseJson);
                    long fileSize = responseData.RootElement.GetProperty("size").GetInt64();

                    var duration = DateTime.Now - startTime;

                    // Registrar en el histórico
                    var backupHistory = new BackupHistory
                    {
                        DatabaseSourceId = databaseId,
                        DatabaseName = databaseName,
                        Date = startTime,
                        Status = "Exitoso",
                        Message = $"Backup guardado exitosamente en OneDrive. Tamaño: {FormatBytes(fileSize)}. Duración: {duration.TotalSeconds:F2} segundos.",
                        BackupPath = oneDrivePath
                    };

                    _context.BackupHistories.Add(backupHistory);
                    await _context.SaveChangesAsync();

                    // Eliminar archivo temporal local
                    try
                    {
                        if (System.IO.File.Exists(localTempPath))
                        {
                            System.IO.File.Delete(localTempPath);
                        }
                    }
                    catch { /* Ignorar errores al eliminar */ }

                    return (true, oneDrivePath, fileSize, string.Empty);
                }
            }
            catch (Exception ex)
            {
                // Error general
                var errorMessage = $"Error al guardar backup en OneDrive: {ex.Message}";

                // Eliminar archivo temporal local si existe
                try
                {
                    if (!string.IsNullOrEmpty(localTempPath) && System.IO.File.Exists(localTempPath))
                    {
                        System.IO.File.Delete(localTempPath);
                    }
                }
                catch { /* Ignorar errores al eliminar */ }

                // Registrar error en el histórico
                try
                {
                    var backupHistory = new BackupHistory
                    {
                        DatabaseSourceId = databaseId,
                        DatabaseName = databaseName,
                        Date = startTime,
                        Status = "Error",
                        Message = errorMessage,
                        BackupPath = oneDrivePath ?? "N/A"
                    };

                    _context.BackupHistories.Add(backupHistory);
                    await _context.SaveChangesAsync();
                }
                catch { /* Ignorar errores al registrar */ }

                return (false, string.Empty, 0, errorMessage);
            }
        }

        /// <summary>
        /// Formatea bytes a una representación legible (KB, MB, GB)
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }

    public class OneDriveListRequest
    {
        public string AccessToken { get; set; }
        public string ItemId { get; set; }
    }

    public class OneDriveCreateFolderRequest
    {
        public string AccessToken { get; set; }
        public string ParentId { get; set; }
        public string FolderName { get; set; }
    }

    public class OneDriveFolderInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime LastModifiedDateTime { get; set; }
    }

    public class SaveTokensRequest
    {
        public int Id { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
