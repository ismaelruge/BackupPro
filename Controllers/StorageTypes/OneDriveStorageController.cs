using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.Services.OAuth;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class OneDriveStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OneDriveSettings _oneDriveSettings;
        private readonly CredentialProtector _credentialProtector;
        private readonly OneDriveTokenService _tokenService;
        private readonly ILogger<OneDriveStorageController> _logger;

        public OneDriveStorageController(ApplicationDbContext context, OneDriveSettings oneDriveSettings, CredentialProtector credentialProtector, OneDriveTokenService tokenService, ILogger<OneDriveStorageController> logger)
        {
            _context = context;
            _oneDriveSettings = oneDriveSettings;
            _credentialProtector = credentialProtector;
            _tokenService = tokenService;
            _logger = logger;
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
        [ValidateAntiForgeryToken]
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
                _logger.LogError(ex, "Error al crear configuración de OneDrive");
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de OneDrive existente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                _logger.LogError(ex, "Error al actualizar configuración de OneDrive");
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de OneDrive.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                _logger.LogError(ex, "Error al eliminar configuración de OneDrive");
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

                // El AccessToken NUNCA se envía al cliente. Para explorar carpetas de una configuración
                // ya guardada, el frontend debe usar ListOneDriveFoldersById/CreateOneDriveFolderById,
                // que resuelven y refrescan el token en el servidor.
                return Json(new {
                    success = true,
                    data = viewModel,
                    hasValidToken = !string.IsNullOrEmpty(oneDrive.AccessToken)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuración de OneDrive {Id}", id);
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Guarda los tokens de OneDrive para una configuración.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTokens([FromBody] SaveTokensRequest request)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(request.Id);
                if (oneDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                oneDrive.AccessToken = _credentialProtector.Protect(request.AccessToken);
                oneDrive.RefreshToken = _credentialProtector.Protect(request.RefreshToken);
                oneDrive.TokenExpiresAt = DateTime.Now.AddHours(1); // Los tokens de Microsoft duran ~1 hora
                oneDrive.LastModifiedAt = DateTime.Now;

                _context.OneDriveStorages.Update(oneDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Tokens guardados exitosamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar tokens de OneDrive");
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

                // Parámetro state anti-CSRF: se guarda en la sesión del usuario que inició el flujo y
                // se valida en el callback para que un atacante no pueda inyectar su propia cuenta.
                var state = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
                HttpContext.Session.SetString("OneDrive_OAuthState", state);

                var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
                              $"client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString("Files.ReadWrite.All offline_access User.Read")}" +
                              $"&response_mode=query" +
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
        public async Task<IActionResult> OneDriveCallback(string code, string? state)
        {
            var expectedState = HttpContext.Session.GetString("OneDrive_OAuthState");
            HttpContext.Session.Remove("OneDrive_OAuthState");

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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ListOneDriveFolders([FromBody] OneDriveListRequest request)
        {
            try
            {
                var (success, folders, message) = await ListOneDriveFoldersInternal(request.AccessToken, request.ItemId);
                return success ? Json(new { success = true, folders }) : Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOneDriveFolder([FromBody] OneDriveCreateFolderRequest request)
        {
            try
            {
                var (success, folderId, message) = await CreateOneDriveFolderInternal(request.AccessToken, request.ParentId, request.FolderName);
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
        /// Lista carpetas de OneDrive para una configuración ya guardada, usando el token almacenado
        /// (descifrado y refrescado en el servidor). El AccessToken nunca llega al cliente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ListOneDriveFoldersById([FromBody] OneDriveListByIdRequest request)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(request.Id);
                if (oneDrive == null || !await _tokenService.EnsureValidTokenAsync(oneDrive))
                {
                    return Json(new { success = false, message = "No hay un token de acceso válido para OneDrive. Por favor, autentícate primero." });
                }

                string accessToken = _tokenService.GetPlainAccessToken(oneDrive);
                var (success, folders, message) = await ListOneDriveFoldersInternal(accessToken, request.ItemId);
                return success ? Json(new { success = true, folders }) : Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar carpetas de OneDrive para {Id}", request.Id);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Crea una carpeta en OneDrive para una configuración ya guardada, usando el token
        /// almacenado (descifrado y refrescado en el servidor). El AccessToken nunca llega al cliente.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOneDriveFolderById([FromBody] OneDriveCreateFolderByIdRequest request)
        {
            try
            {
                var oneDrive = await _context.OneDriveStorages.FindAsync(request.Id);
                if (oneDrive == null || !await _tokenService.EnsureValidTokenAsync(oneDrive))
                {
                    return Json(new { success = false, message = "No hay un token de acceso válido para OneDrive. Por favor, autentícate primero." });
                }

                string accessToken = _tokenService.GetPlainAccessToken(oneDrive);
                var (success, folderId, message) = await CreateOneDriveFolderInternal(accessToken, request.ParentId, request.FolderName);
                return success
                    ? Json(new { success = true, folderId, message = "Carpeta creada exitosamente" })
                    : Json(new { success = false, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear carpeta de OneDrive para {Id}", request.Id);
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        private static async Task<(bool success, List<OneDriveFolderInfo> folders, string message)> ListOneDriveFoldersInternal(string accessToken, string? itemId)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var url = string.IsNullOrEmpty(itemId)
                ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
                : $"https://graph.microsoft.com/v1.0/me/drive/items/{itemId}/children";

            // Filtrar solo carpetas
            url += "?$filter=folder ne null&$select=id,name,lastModifiedDateTime,folder&$orderby=name";

            var response = await httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, new List<OneDriveFolderInfo>(), $"Error al listar carpetas: {json}");
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

            return (true, folders, string.Empty);
        }

        private static async Task<(bool success, string? folderId, string message)> CreateOneDriveFolderInternal(string accessToken, string? parentId, string folderName)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var url = string.IsNullOrEmpty(parentId)
                ? "https://graph.microsoft.com/v1.0/me/drive/root/children"
                : $"https://graph.microsoft.com/v1.0/me/drive/items/{parentId}/children";

            // Se usa un Dictionary para poder incluir la clave "@microsoft.graph.conflictBehavior"
            // (no es un identificador C# válido) y para que folderName quede correctamente escapado.
            var payload = new Dictionary<string, object>
            {
                ["name"] = folderName,
                ["folder"] = new { },
                ["@microsoft.graph.conflictBehavior"] = "fail"
            };
            var jsonBody = System.Text.Json.JsonSerializer.Serialize(payload);

            var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"Error al crear carpeta: {responseJson}");
            }

            var data = System.Text.Json.JsonDocument.Parse(responseJson);
            return (true, data.RootElement.GetProperty("id").GetString(), string.Empty);
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

    public class OneDriveListByIdRequest
    {
        public int Id { get; set; }
        public string? ItemId { get; set; }
    }

    public class OneDriveCreateFolderByIdRequest
    {
        public int Id { get; set; }
        public string? ParentId { get; set; }
        public string FolderName { get; set; } = string.Empty;
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
