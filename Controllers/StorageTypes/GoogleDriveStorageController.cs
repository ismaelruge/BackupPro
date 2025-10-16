using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class GoogleDriveStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public GoogleDriveStorageController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
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
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Actualiza una configuración de Google Drive existente.
        /// </summary>
        [HttpPost]
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
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de Google Drive.
        /// </summary>
        [HttpPost]
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

                // Incluir AccessToken para permitir exploración sin reconectar
                // TODO: En producción, verificar expiración antes de enviar
                return Json(new {
                    success = true,
                    data = viewModel,
                    accessToken = googleDrive.AccessToken, // Para usar el explorador sin reconectar
                    hasValidToken = !string.IsNullOrEmpty(googleDrive.AccessToken)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Guarda los tokens de Google Drive para una configuración.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveTokens([FromBody] GoogleDriveSaveTokensRequest request)
        {
            try
            {
                var googleDrive = await _context.GoogleDriveStorages.FindAsync(request.Id);
                if (googleDrive == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // TODO: En producción, encriptar los tokens antes de guardarlos
                googleDrive.AccessToken = request.AccessToken;
                googleDrive.RefreshToken = request.RefreshToken;
                googleDrive.TokenExpiresAt = DateTime.Now.AddHours(1); // Los tokens de Google duran ~1 hora
                googleDrive.LastModifiedAt = DateTime.Now;

                _context.GoogleDriveStorages.Update(googleDrive);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Tokens guardados exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al guardar tokens: {ex.Message}" });
            }
        }

        // ========== GOOGLE DRIVE ENDPOINTS ==========

        [HttpGet]
        public IActionResult GoogleDriveAuth()
        {
            try
            {
                var clientId = _configuration["GoogleOAuth:ClientId"];
                var redirectUri = _configuration["GoogleOAuth:RedirectUri"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
                {
                    return BadRequest("Google OAuth configuration is missing");
                }

                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                              $"client_id={clientId}" +
                              $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                              $"&response_type=code" +
                              $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/drive.readonly https://www.googleapis.com/auth/drive.file https://www.googleapis.com/auth/userinfo.email")}" +
                              $"&access_type=offline" +
                              $"&prompt=consent";

                return Redirect(authUrl);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error during authentication: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GoogleDriveCallback(string code)
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
                var clientId = _configuration["GoogleOAuth:ClientId"];
                var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
                var redirectUri = _configuration["GoogleOAuth:RedirectUri"];

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

                var userInfoResponse = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v2/userinfo?access_token={accessToken}");
                var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
                var userInfo = System.Text.Json.JsonDocument.Parse(userInfoJson);
                var email = userInfo.RootElement.GetProperty("email").GetString();

                // Guardar tokens en sesión (temporal) - con manejo de errores
                try
                {
                    HttpContext.Session.SetString($"GoogleDrive_AccessToken_{email}", accessToken!);
                    if (tokenData.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement))
                    {
                        HttpContext.Session.SetString($"GoogleDrive_RefreshToken_{email}", refreshTokenElement.GetString()!);
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

                return Json(new { success = true, folders = folders });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateDriveFolder([FromBody] DriveCreateFolderRequest request)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.AccessToken}");

                var parentId = string.IsNullOrEmpty(request.ParentId) ? "root" : request.ParentId;
                var folderMetadata = new
                {
                    name = request.FolderName,
                    mimeType = "application/vnd.google-apps.folder",
                    parents = new[] { parentId }
                };

                var jsonContent = System.Text.Json.JsonSerializer.Serialize(folderMetadata);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync("https://www.googleapis.com/drive/v3/files", content);
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
    public class DriveListRequest
    {
        public string AccessToken { get; set; }
        public string ParentId { get; set; }
    }

    public class DriveCreateFolderRequest
    {
        public string AccessToken { get; set; }
        public string ParentId { get; set; }
        public string FolderName { get; set; }
    }

    public class DriveFolderInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime ModifiedTime { get; set; }
    }

    public class GoogleDriveSaveTokensRequest
    {
        public int Id { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
