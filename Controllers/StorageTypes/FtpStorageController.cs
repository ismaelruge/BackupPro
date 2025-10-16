using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class FtpStorageController : Controller
    {
        private readonly IConfiguration _configuration;

        public FtpStorageController(IConfiguration configuration)
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

}
