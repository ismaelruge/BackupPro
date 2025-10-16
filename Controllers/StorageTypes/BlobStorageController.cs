using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers.StorageTypes
{
    /// <summary>
    /// Controlador para la gestión de configuraciones de Azure Blob Storage.
    /// </summary>
    [Authorize]
    public class BlobStorageController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlobStorageController(ApplicationDbContext context)
        {
            _context = context;
        }

        #region Métodos CRUD

        /// <summary>
        /// Lista todas las configuraciones de Azure Blob Storage registradas.
        /// </summary>
        /// <returns>Vista con la lista de configuraciones.</returns>
        public async Task<IActionResult> Index()
        {
            var list = await _context.AzureBlobStorages.ToListAsync();
            return View(list);
        }

        /// <summary>
        /// Procesa la creación de una nueva configuración de Azure Blob Storage.
        /// </summary>
        /// <param name="model">Datos de la configuración a crear.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AzureBlobStorageViewModel model)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Verificar si ya existe una configuración con el mismo nombre
                    bool exists = await _context.AzureBlobStorages.AnyAsync(b => b.ConfigurationName == model.ConfigurationName);

                    if (exists)
                    {
                        return Json(new { success = false, message = "Ya existe una configuración con este nombre." });
                    }

                    var blobStorage = new AzureBlobStorage
                    {
                        ConfigurationName = model.ConfigurationName,
                        AccountName = model.AccountName,
                        ConnectionString = model.ConnectionString, // TODO: Encriptar en producción
                        ContainerName = model.ContainerName,
                        BlobPrefix = model.BlobPrefix ?? string.Empty,
                        CreatedAt = DateTime.Now,
                        CreatedBy = User.Identity?.Name
                    };

                    _context.AzureBlobStorages.Add(blobStorage);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Configuración de Azure Blob Storage creada exitosamente." });
                }

                return Json(new { success = false, message = "Datos inválidos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al crear configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Procesa la edición de una configuración de Azure Blob Storage.
        /// </summary>
        /// <param name="model">Datos editados de la configuración.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] AzureBlobStorageViewModel model)
        {
            try
            {
                if (!model.Id.HasValue)
                {
                    return Json(new { success = false, message = "ID de configuración no especificado." });
                }

                var blobStorage = await _context.AzureBlobStorages.FindAsync(model.Id.Value);
                if (blobStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                // Verificar si otro registro tiene el mismo nombre
                bool exists = await _context.AzureBlobStorages.AnyAsync(b => b.ConfigurationName == model.ConfigurationName && b.Id != model.Id);
                if (exists)
                {
                    return Json(new { success = false, message = "Ya existe otra configuración con este nombre." });
                }

                blobStorage.ConfigurationName = model.ConfigurationName;
                blobStorage.AccountName = model.AccountName;
                blobStorage.ConnectionString = model.ConnectionString; // TODO: Encriptar en producción
                blobStorage.ContainerName = model.ContainerName;
                blobStorage.BlobPrefix = model.BlobPrefix ?? string.Empty;
                blobStorage.LastModifiedAt = DateTime.Now;

                _context.AzureBlobStorages.Update(blobStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al actualizar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Elimina una configuración de Azure Blob Storage por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración a eliminar.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var blobStorage = await _context.AzureBlobStorages.FindAsync(id);
                if (blobStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                _context.AzureBlobStorages.Remove(blobStorage);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Configuración eliminada exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al eliminar configuración: {ex.Message}" });
            }
        }

        /// <summary>
        /// Obtiene los datos de una configuración de Azure Blob Storage por su Id.
        /// </summary>
        /// <param name="id">Id de la configuración.</param>
        /// <returns>Resultado JSON con los datos de la configuración.</returns>
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var blobStorage = await _context.AzureBlobStorages.FindAsync(id);
                if (blobStorage == null)
                {
                    return Json(new { success = false, message = "Configuración no encontrada." });
                }

                var viewModel = new AzureBlobStorageViewModel
                {
                    Id = blobStorage.Id,
                    ConfigurationName = blobStorage.ConfigurationName,
                    AccountName = blobStorage.AccountName,
                    ConnectionString = blobStorage.ConnectionString,
                    ContainerName = blobStorage.ContainerName,
                    BlobPrefix = blobStorage.BlobPrefix
                };

                return Json(new { success = true, data = viewModel });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error al obtener configuración: {ex.Message}" });
            }
        }

        #endregion

        #region Métodos auxiliares Azure Blob

        /// <summary>
        /// Crea un nuevo directorio en Azure Blob Storage.
        /// </summary>
        /// <param name="request">Datos para la creación del directorio.</param>
        /// <returns>Resultado JSON con éxito o error.</returns>
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

                return Json(new
                {
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

        #endregion
    }

    #region Clases de soporte

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

    #endregion
}
