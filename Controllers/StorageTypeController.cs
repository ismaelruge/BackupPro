using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentFTP;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace BackupPro.Controllers
{
    [Authorize]
    public class StorageTypeController : Controller
    {
        private readonly IConfiguration _configuration;

        public StorageTypeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ListFtpDirectories([FromBody] FtpConnectionRequest request)
        {
            try
            {
                // Parsear host y puerto
                var hostParts = request.Host.Split(':');
                var host = hostParts[0];
                var port = hostParts.Length > 1 && int.TryParse(hostParts[1], out int p) ? p : 21;

                // Crear cliente FTP
                var client = new AsyncFtpClient(host, request.Username, request.Password, port);

                try
                {
                    // Conectar al servidor
                    await client.Connect();

                    // Obtener la ruta a listar (si no se especifica, usar raíz)
                    var path = string.IsNullOrEmpty(request.Path) ? "/" : request.Path;

                    // Listar directorios
                    var items = await client.GetListing(path);

                    // Filtrar solo directorios
                    var directories = items
                        .Where(item => item.Type == FtpObjectType.Directory)
                        .Select(item => new FtpDirectoryInfo
                        {
                            Name = item.Name,
                            FullPath = item.FullName,
                            ModifiedDate = item.Modified
                        })
                        .OrderBy(d => d.Name)
                        .ToList();

                    await client.Disconnect();

                    return Json(new { success = true, directories = directories, currentPath = path });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error al listar directorios: {ex.Message}" });
                }
                finally
                {
                    client?.Dispose();
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error de conexión: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateFtpDirectory([FromBody] FtpCreateDirectoryRequest request)
        {
            try
            {
                // Parsear host y puerto
                var hostParts = request.Host.Split(':');
                var host = hostParts[0];
                var port = hostParts.Length > 1 && int.TryParse(hostParts[1], out int p) ? p : 21;

                // Crear cliente FTP
                var client = new AsyncFtpClient(host, request.Username, request.Password, port);

                try
                {
                    // Conectar al servidor
                    await client.Connect();

                    // Construir la ruta completa
                    var fullPath = string.IsNullOrEmpty(request.CurrentPath) || request.CurrentPath == "/"
                        ? $"/{request.NewFolderName}"
                        : $"{request.CurrentPath.TrimEnd('/')}/{request.NewFolderName}";

                    // Crear el directorio
                    await client.CreateDirectory(fullPath);

                    await client.Disconnect();

                    return Json(new { success = true, message = "Carpeta creada exitosamente", newPath = fullPath });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = $"Error al crear carpeta: {ex.Message}" });
                }
                finally
                {
                    client?.Dispose();
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error de conexión: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBlobDirectory([FromBody] BlobCreateDirectoryRequest request)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(request.ConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(request.ContainerName);

                // Construir la ruta completa de la nueva carpeta
                var currentPath = string.IsNullOrEmpty(request.CurrentPath) || request.CurrentPath == "/"
                    ? ""
                    : request.CurrentPath.TrimEnd('/') + "/";

                var fullPath = currentPath + request.NewFolderName + "/";

                // En Blob Storage, las carpetas se crean subiendo un archivo "marcador" vacío
                // El archivo se llama .folder y se usa para crear la estructura de carpetas
                var blobClient = containerClient.GetBlobClient(fullPath + ".folder");

                // Crear un blob vacío para representar la carpeta
                using (var stream = new System.IO.MemoryStream(new byte[0]))
                {
                    await blobClient.UploadAsync(stream, overwrite: false);
                }

                return Json(new { success = true, message = "Carpeta creada exitosamente", newPath = fullPath.TrimEnd('/') });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear carpeta: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ListBlobContainers([FromBody] BlobConnectionRequest request)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(request.ConnectionString);

                var containers = new List<BlobContainerInfo>();

                await foreach (var containerItem in blobServiceClient.GetBlobContainersAsync())
                {
                    containers.Add(new BlobContainerInfo
                    {
                        Name = containerItem.Name,
                        LastModified = containerItem.Properties.LastModified.DateTime
                    });
                }

                return Json(new { success = true, containers = containers.OrderBy(c => c.Name).ToList() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al listar contenedores: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ListBlobDirectories([FromBody] BlobDirectoryRequest request)
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(request.ConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(request.ContainerName);

                var directories = new List<BlobDirectoryInfo>();
                var delimiter = "/";
                var prefix = string.IsNullOrEmpty(request.Path) ? "" : request.Path.TrimEnd('/') + "/";

                await foreach (var item in containerClient.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: delimiter))
                {
                    if (item.IsPrefix)
                    {
                        var dirName = item.Prefix.TrimEnd('/');
                        var displayName = dirName.Substring(dirName.LastIndexOf('/') + 1);

                        directories.Add(new BlobDirectoryInfo
                        {
                            Name = displayName,
                            FullPath = item.Prefix.TrimEnd('/')
                        });
                    }
                }

                return Json(new {
                    success = true,
                    directories = directories.OrderBy(d => d.Name).ToList(),
                    currentPath = prefix.TrimEnd('/')
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al listar directorios: {ex.Message}" });
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

    // Clases de soporte
    public class FtpConnectionRequest
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Path { get; set; }
    }

    public class FtpCreateDirectoryRequest
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string CurrentPath { get; set; }
        public string NewFolderName { get; set; }
    }

    public class FtpDirectoryInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public DateTime ModifiedDate { get; set; }
    }

    public class BlobConnectionRequest
    {
        public string ConnectionString { get; set; }
    }

    public class BlobCreateDirectoryRequest
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
        public string CurrentPath { get; set; }
        public string NewFolderName { get; set; }
    }

    public class BlobDirectoryRequest
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; }
        public string Path { get; set; }
    }

    public class BlobContainerInfo
    {
        public string Name { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class BlobDirectoryInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
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
