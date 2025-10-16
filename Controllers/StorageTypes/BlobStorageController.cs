using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackupPro.Controllers.StorageTypes
{
    [Authorize]
    public class BlobStorageController : Controller
    {
        private readonly IConfiguration _configuration;

        public BlobStorageController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
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
}
