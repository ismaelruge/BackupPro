using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class OneDriveStorageController : Controller
    {
        private readonly IConfiguration _configuration;

        public OneDriveStorageController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        // ========== ONEDRIVE ENDPOINTS ==========

        [HttpGet]
        public IActionResult OneDriveAuth()
        {
            try
            {
                var clientId = _configuration["OneDrive:ClientId"];
                var redirectUri = _configuration["OneDrive:RedirectUri"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return BadRequest("OneDrive OAuth configuration is missing");
                }

                var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?" +
                              $"client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString("Files.ReadWrite.All offline_access User.Read")}" +
                              $"&response_mode=query";

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
                var clientId = _configuration["OneDrive:ClientId"];
                var clientSecret = _configuration["OneDrive:ClientSecret"];
                var redirectUri = _configuration["OneDrive:RedirectUri"];

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
                    if (tokenData.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
                    {
                        HttpContext.Session.SetString($"OneDrive_RefreshToken_{email}", refreshTokenElement.GetString()!);
                    }
                }
                catch (Exception sessionEx)
                {
                    Console.WriteLine($"No se pudo guardar en sesión: {sessionEx.Message}");
                }

                // Escapar valores para JavaScript
                var safeEmail = System.Text.Json.JsonSerializer.Serialize(email);
                var safeAccessToken = System.Text.Json.JsonSerializer.Serialize(accessToken);

                // Retornar éxito con el email
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
                                        accessToken: {safeAccessToken}
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
}
