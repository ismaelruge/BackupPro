using FluentFTP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers.StorageTypes
{
    /// <summary>
    /// Controlador para la gestión de configuraciones de almacenamiento FTP.
    /// </summary>
    [Authorize]
    public class FtpStorageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public FtpStorageController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        #region Métodos CRUD

        /// <summary>
        /// Lista todas las configuraciones de FTP registradas.
        /// </summary>
        /// <returns>Vista con la lista de configuraciones.</returns>
        public async Task<IActionResult> Index()
        {
            var list = await _context.FtpStorages.ToListAsync();
            return View(list);
        }

        /// <summary>
        /// Procesa la creación de una nueva configuración FTP.
        /// </summary>
        /// <param name="model">Datos de la configuración a crear.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] FtpStorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.FtpStorages.AnyAsync(f => f.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var ftpStorage = new FtpStorage
                    {
                        ConfigurationName = model.ConfigurationName,
                        Host = model.Host,
                        Port = model.Port,
                        Username = model.Username,
                        Password = model.Password, // TODO: Encriptar en producción
                        RemotePath = model.RemotePath,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.FtpStorages.Add(ftpStorage);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración FTP creada exitosamente." });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Procesa la edición de una configuración FTP.
        /// </summary>
        /// <param name="model">Datos editados de la configuración.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] FtpStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var ftpStorage = await _context.FtpStorages.FindAsync(model.Id.Value);
                if (ftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.FtpStorages.AnyAsync(f => f.ConfigurationName == model.ConfigurationName && f.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                ftpStorage.ConfigurationName = model.ConfigurationName;
                ftpStorage.Host = model.Host;
                ftpStorage.Port = model.Port;
                ftpStorage.Username = model.Username;
                ftpStorage.Password = model.Password; // TODO: Encriptar en producción
                ftpStorage.RemotePath = model.RemotePath;
                ftpStorage.LastModifiedAt = DateTime.Now;

                _context.FtpStorages.Update(ftpStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración FTP por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración a eliminar.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ftpStorage = await _context.FtpStorages.FindAsync(id);
                if (ftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.FtpStorages.Remove(ftpStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los datos de una configuración FTP por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración.</param>
        /// <returns>Resultado JSON con los datos de la configuración.</returns>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var ftpStorage = await _context.FtpStorages.FindAsync(id);
                if (ftpStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new FtpStorageViewModel
                {
                    Id = ftpStorage.Id,
                    ConfigurationName = ftpStorage.ConfigurationName,
                    Host = ftpStorage.Host,
                    Port = ftpStorage.Port,
                    Username = ftpStorage.Username,
                    Password = ftpStorage.Password, // En edición se puede mostrar u ocultar
                    RemotePath = ftpStorage.RemotePath
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        #endregion

        #region Métodos auxiliares FTP

        /// <summary>
        /// Lista los directorios disponibles en un servidor FTP.
        /// </summary>
        /// <param name="request">Datos de conexión FTP.</param>
        /// <returns>Resultado JSON con lista de directorios.</returns>
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

        /// <summary>
        /// Crea un nuevo directorio en el servidor FTP.
        /// </summary>
        /// <param name="request">Datos para la creación del directorio.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
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

        #endregion
    }

    #region Clases de soporte
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

    #endregion
}
