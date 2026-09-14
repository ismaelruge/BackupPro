using System.IO.Compression;
using System.Text;
using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Genera un backup de una base de datos MongoDB configurada usando mongodump.</summary>
    public class MongoDbBackupProvider : IDatabaseBackupProvider
    {
        private static readonly string[] WindowsCommonPaths =
        {
            @"C:\Program Files\MongoDB\Server\8.2\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\8.1\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\8.0\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Tools\100\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\7.0\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\6.0\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\5.0\bin\mongodump.exe",
            @"C:\Program Files\MongoDB\Server\4.4\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\8.2\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\8.1\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\8.0\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Tools\100\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\7.0\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\6.0\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\5.0\bin\mongodump.exe",
            @"C:\Program Files (x86)\MongoDB\Server\4.4\bin\mongodump.exe"
        };

        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<MongoDbBackupProvider> _logger;

        public string DatabaseType => "MongoDB";

        public MongoDbBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<MongoDbBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;
            string tempArchivePath = string.Empty;

            try
            {
                var mongodbConfig = await _context.MongoDBDataBases.FindAsync(databaseId);
                if (mongodbConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de MongoDB no encontrada");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFolderName = $"{mongodbConfig.DatabaseName}_backup_{timestamp}";
                string fileName = $"{backupFolderName}.zip";

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                    .OrderByDescending(d => d.AvailableFreeSpace)
                    .ToList();

                if (drives.Count == 0)
                {
                    return (false, null, string.Empty, string.Empty, "No se encontraron discos disponibles para crear el backup temporal");
                }

                string tempFolder = Path.Combine(drives[0].Name, "Temp");

                if (!Directory.Exists(tempFolder))
                {
                    Directory.CreateDirectory(tempFolder);
                }

                tempBackupPath = Path.Combine(tempFolder, backupFolderName);
                tempArchivePath = Path.Combine(tempFolder, fileName);

                string mongoDumpPath = ExecutableLocator.Find("mongodump", WindowsCommonPaths, "mongodump");
                if (string.IsNullOrEmpty(mongoDumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró mongodump. Asegúrate de que MongoDB Database Tools esté instalado y mongodump esté en el PATH del sistema.");
                }

                string? plainPassword = _credentialProtector.Unprotect(mongodbConfig.Password);
                bool hasCredentials = !string.IsNullOrWhiteSpace(mongodbConfig.Username) && !string.IsNullOrWhiteSpace(plainPassword);

                var arguments = new StringBuilder();

                // Las credenciales, si existen, se pasan mediante un archivo de configuración temporal
                // (--config) en vez de --username/--password en la línea de comandos, para que no
                // queden expuestas en la lista de procesos del sistema (ps/Task Manager). El archivo
                // usa claves planas (no anidadas), como corresponde a los nombres largos de los flags.
                string? credentialsFilePath = null;
                if (hasCredentials)
                {
                    credentialsFilePath = Path.Combine(Path.GetTempPath(), $"mongodump_{Guid.NewGuid():N}.yaml");
                    string yaml = $"username: \"{mongodbConfig.Username}\"\n" +
                                  $"password: \"{plainPassword}\"\n" +
                                  "authenticationDatabase: \"admin\"\n";
                    await File.WriteAllTextAsync(credentialsFilePath, yaml);

                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }

                    arguments.Append($"--config=\"{credentialsFilePath}\" ");
                }

                arguments.Append($"--host={mongodbConfig.Host} ");
                arguments.Append($"--port={mongodbConfig.Port} ");
                arguments.Append($"--db={mongodbConfig.DatabaseName} ");
                arguments.Append($"--out=\"{tempBackupPath}\" ");

                if (mongodbConfig.SslEnabled)
                {
                    arguments.Append("--ssl ");
                }

                try
                {
                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mongoDumpPath,
                        Arguments = arguments.ToString(),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso mongodump");
                        }

                        bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                        if (!exited)
                        {
                            process.Kill();
                            return (false, null, string.Empty, string.Empty, "El proceso mongodump excedió el tiempo límite de 5 minutos");
                        }

                        string errorOutput = await process.StandardError.ReadToEndAsync();

                        if (process.ExitCode != 0)
                        {
                            return (false, null, string.Empty, string.Empty, $"Error al ejecutar mongodump: {errorOutput}");
                        }
                    }
                }
                finally
                {
                    if (credentialsFilePath != null)
                    {
                        try
                        {
                            File.Delete(credentialsFilePath);
                        }
                        catch { /* Ignorar errores al eliminar el archivo de credenciales temporal */ }
                    }
                }

                if (!Directory.Exists(tempBackupPath))
                {
                    return (false, null, string.Empty, string.Empty, "El directorio de backup no se creó correctamente");
                }

                ZipFile.CreateFromDirectory(tempBackupPath, tempArchivePath);

                if (!File.Exists(tempArchivePath))
                {
                    return (false, null, string.Empty, string.Empty, "El archivo ZIP del backup no se creó correctamente");
                }

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempArchivePath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                memoryStream.Position = 0;
                TryDeleteTempFiles(tempBackupPath, tempArchivePath);

                return (true, memoryStream, fileName, mongodbConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteTempFiles(tempBackupPath, tempArchivePath);
                _logger.LogError(ex, "Error al crear backup de MongoDB {DatabaseId}", databaseId);
                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        private static void TryDeleteTempFiles(string folderPath, string archivePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    Directory.Delete(folderPath, true);
                }
                if (!string.IsNullOrEmpty(archivePath) && File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
            }
            catch { /* Ignorar errores al eliminar archivos temporales */ }
        }
    }
}
