using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.Services.OAuth;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class GoogleDriveStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleOAuthSettings _googleOAuthSettings;
        private readonly CredentialProtector _credentialProtector;
        private readonly GoogleDriveTokenService _tokenService;
        private readonly ILogger<GoogleDriveStorageController> _logger;

        public GoogleDriveStorageController(ApplicationDbContext context, GoogleOAuthSettings googleOAuthSettings, CredentialProtector credentialProtector, GoogleDriveTokenService tokenService, ILogger<GoogleDriveStorageController> logger)
        {
            _context = context;
            _googleOAuthSettings = googleOAuthSettings;
            _credentialProtector = credentialProtector;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _context.GoogleDriveStorages.ToListAsync();
            return View(list);
        }

        // ========== CRUD OPERATIONS ==========

        /// <summary>
        /// Crea una nueva configuración de Google Drive.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] GoogleDriveStorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.GoogleDriveStorages.AnyAsync(g => g.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var googleDrive = new GoogleDriveStorage
                    {
                        ConfigurationName = model.ConfigurationName,
                        Email = model.Email,
                        FolderId = model.FolderId ?? string.Empty,
                        FolderPath = model.FolderPath ?? string.Empty,
                        AccessToken = null, // Se guardará después de la autenticación
                        RefreshToken = null,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.GoogleDriveStorages.Add(googleDrive);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de Google Drive creada exitosamente.", id = googleDrive.Id });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear configuración de Google Drive");
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de Google Drive existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] GoogleDriveStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var googleDrive = await _context.GoogleDriveStorages.FindAsync(model.Id.Value);
                if (googleDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.GoogleDriveStorages.AnyAsync(g => g.ConfigurationName == model.ConfigurationName && g.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                googleDrive.ConfigurationName = model.ConfigurationName;
                googleDrive.Email = model.Email;
                googleDrive.FolderId = model.FolderId ?? string.Empty;
                googleDrive.FolderPath = model.FolderPath ?? string.Empty;
                googleDrive.LastModifiedAt = DateTime.Now;

                _context.GoogleDriveStorages.Update(googleDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración de Google Drive");
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de Google Drive.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(id);
                if (googleDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.GoogleDriveStorages.Remove(googleDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar configuración de Google Drive");
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene una configuración de Google Drive por su ID.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(id);
                if (googleDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new GoogleDriveStorageViewModel
                {
                    Id = googleDrive.Id,
                    ConfigurationName = googleDrive.ConfigurationName,
                    Email = googleDrive.Email,
                    FolderId = googleDrive.FolderId,
                    FolderPath = googleDrive.FolderPath,
                    TokenExpiresAt = googleDrive.TokenExpiresAt
                };

                // El AccessToken NUNCA se envía al cliente. Para explorar carpetas de una configuración
                // ya guardada, el frontend debe usar ListDriveFoldersById/CreateDriveFolderById, que
                // resuelven y refrescan el token en el servidor.
                return Json(new {
                    success = true,
                    data = viewModel,
                    hasValidToken = !string.IsNullOrEmpty(googleDrive.AccessToken)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuración de Google Drive {Id}", id);
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Guarda los tokens de Google Drive para una configuración.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTokens([FromBody] GoogleDriveSaveTokensRequest request)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(request.Id);
                if (googleDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                googleDrive.AccessToken = _credentialProtector.Protect(request.AccessToken);
                googleDrive.RefreshToken = _credentialProtector.Protect(request.RefreshToken);
                googleDrive.TokenExpiresAt = DateTime.Now.AddHours(1); // Los tokens de Google duran ~1 hora
                googleDrive.LastModifiedAt = DateTime.Now;

                _context.GoogleDriveStorages.Update(googleDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Tokens guardados exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar tokens de Google Drive");
                return Json(new { success = false, message = $"Error al guardar tokens: {ex.Message}" });
            }
        }

        // ========== GOOGLE DRIVE ENDPOINTS ==========

        [HttpGet]
        public IActionResult GoogleDriveAuth()
        {
            try
            {
                var clientId = _googleOAuthSettings.ClientId;
                var redirectUri = _googleOAuthSettings.RedirectUri;

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return BadRequest("Google OAuth configuration is missing");
                }

                // Parámetro state anti-CSRF: se guarda en la sesión del usuario que inició el flujo y
                // se valida en el callback para que un atacante no pueda inyectar su propia cuenta.
                var state = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                HttpContext.Session.SetString("GoogleDrive_OAuthState", state);

                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                              $"client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/drive.readonly https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email")}" +
                              $"&access_type=offline" +
                              $"&prompt=consent" +
                              $"&state={Uri.EscapeDataString(state)}";

                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error during authentication: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GoogleDriveCallback(string code, string? state)
        {
            var expectedState = HttpContext.Session.GetString("GoogleDrive_OAuthState");
            HttpContext.Session.Remove("GoogleDrive_OAuthState");

            if (string.IsNullOrEmpty(expectedState) || !string.Equals(expectedState, state, StringComparison.Ordinal))
            {
                return Content("<html><body><p>Solicitud de autenticación inválida o expirada. Cierra esta ventana e inténtalo de nuevo.</p></body></html>", "text/html");
            }

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
                var clientId = _googleOAuthSettings.ClientId;
                var clientSecret = _googleOAuthSettings.ClientSecret;
                var redirectUri = _googleOAuthSettings.RedirectUri;

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
                var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
                    new FormUrlEncodedContent(tokenRequest));

                var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    var errorMsg = System.Text.Json.JsonSerializer.Serialize(tokenJson).Replace("'", "\\'");
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

                var userInfoResponse = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v2/userinfo?access_token={accessToken}");
                var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                var userInfo = System.Text.Json.JsonDocument.Parse(userInfoJson);
                var email = userInfo.RootElement.GetProperty("email").GetString();

                // Guardar tokens en sesión (temporal) - con manejo de errores
                try
                {
                    HttpContext.Session.SetString($"GoogleDrive_AccessToken_{email}", accessToken!);
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        HttpContext.Session.SetString($"GoogleDrive_RefreshToken_{email}", refreshToken);
                    }
                }
                catch (Exception sessionEx)
                {
                    // Si falla el guardado en sesión, continuar de todas formas
                    // El token se enviará al frontend igualmente
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ListDriveFolders([FromBody] DriveListRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.AccessToken}");

                var parentId = string.IsNullOrEmpty(request.ParentId) ? "root" : request.ParentId;
                var query = $"'{parentId}' in parents and mimeType='application/vnd.google-apps.folder' and trashed=false";
                var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime)&orderBy=name";

                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error al listar carpetas: {json}" });
                }

                return Json(new { success = true, folders = ParseDriveFolders(json) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriveFolder([FromBody] DriveCreateFolderRequest request)
        {
            try
            {
                var (success, folderId, message) = await CreateDriveFolderInternal(request.AccessToken, request.ParentId, request.FolderName);
                return success
                    ? Json(new { success = true, folderId, message = "Carpeta creada exitosamente" })
                    : Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lista carpetas de Google Drive para una configuración ya guardada, usando el token
        /// almacenado (descifrado y refrescado en el servidor). El AccessToken nunca llega al cliente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ListDriveFoldersById([FromBody] DriveListByIdRequest request)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(request.Id);
                if (googleDrive == null || !await _tokenService.EnsureValidTokenAsync(googleDrive))
                {
                    return Json(new { success = false, message = "No hay un token de acceso válido para Google Drive. Por favor, autentícate primero." });
                }

                string accessToken = _tokenService.GetPlainAccessToken(googleDrive);

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var parentId = string.IsNullOrEmpty(request.ParentId) ? "root" : request.ParentId;
                var query = $"'{parentId}' in parents and mimeType='application/vnd.google-apps.folder' and trashed=false";
                var url = $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name,modifiedTime)&orderBy=name";

                var response = await httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = $"Error al listar carpetas: {json}" });
                }

                return Json(new { success = true, folders = ParseDriveFolders(json) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar carpetas de Google Drive para {Id}", request.Id);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Crea una carpeta en Google Drive para una configuración ya guardada, usando el token
        /// almacenado (descifrado y refrescado en el servidor). El AccessToken nunca llega al cliente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDriveFolderById([FromBody] DriveCreateFolderByIdRequest request)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(request.Id);
                if (googleDrive == null || !await _tokenService.EnsureValidTokenAsync(googleDrive))
                {
                    return Json(new { success = false, message = "No hay un token de acceso válido para Google Drive. Por favor, autentícate primero." });
                }

                string accessToken = _tokenService.GetPlainAccessToken(googleDrive);
                var (success, folderId, message) = await CreateDriveFolderInternal(accessToken, request.ParentId, request.FolderName);
                return success
                    ? Json(new { success = true, folderId, message = "Carpeta creada exitosamente" })
                    : Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear carpeta de Google Drive para {Id}", request.Id);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private static List<DriveFolderInfo> ParseDriveFolders(string json)
        {
            var data = System.Text.Json.JsonDocument.Parse(json);
            var folders = new List<DriveFolderInfo>();

            if (data.RootElement.TryGetProperty("files", out var filesElement))
            {
                foreach (var file in filesElement.EnumerateArray())
                {
                    folders.Add(new DriveFolderInfo
                    {
                        Id = file.GetProperty("id").GetString()!,
                        Name = file.GetProperty("name").GetString()!,
                        ModifiedTime = file.TryGetProperty("modifiedTime", out var modTime)
                            ? DateTime.Parse(modTime.GetString()!)
                            : DateTime.MinValue
                    });
                }
            }

            return folders;
        }

        private static async Task<(bool success, string? folderId, string message)> CreateDriveFolderInternal(string accessToken, string? parentId, string folderName)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var effectiveParentId = string.IsNullOrEmpty(parentId) ? "root" : parentId;
            var folderMetadata = new
            {
                name = folderName,
                mimeType = "application/vnd.google-apps.folder",
                parents = new[] { effectiveParentId }
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(folderMetadata);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://www.googleapis.com/drive/v3/files", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Error al crear carpeta: {responseJson}");
            }

            var data = System.Text.Json.JsonDocument.Parse(responseJson);
            return (true, data.RootElement.GetProperty("id").GetString(), string.Empty);
        }
    }

    public class DriveListRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
    }

    public class DriveCreateFolderRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string ParentId { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
    }

    public class DriveListByIdRequest
    {
        public int Id { get; set; }
        public string? ParentId { get; set; }
    }

    public class DriveCreateFolderByIdRequest
    {
        public int Id { get; set; }
        public string? ParentId { get; set; }
        public string FolderName { get; set; } = string.Empty;
    }

    public class DriveFolderInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime ModifiedTime { get; set; }
    }

    public class GoogleDriveSaveTokensRequest
    {
        public int Id { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
