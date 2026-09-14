using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Genera un backup de una base de datos MariaDB configurada usando mariadb-dump (o, si no está
    /// disponible, mysqldump: MariaDB es compatible con el protocolo y las herramientas de MySQL).
    /// </summary>
    public class MariaDbBackupProvider : IDatabaseBackupProvider
    {
        private static readonly string[] MariaDbDumpWindowsPaths =
        {
            @"C:\Program Files\MariaDB 11.4\bin\mariadb-dump.exe",
            @"C:\Program Files\MariaDB 11.0\bin\mariadb-dump.exe",
            @"C:\Program Files\MariaDB 10.11\bin\mariadb-dump.exe",
            @"C:\Program Files\MariaDB 10.6\bin\mariadb-dump.exe",
            @"C:\Program Files (x86)\MariaDB 11.4\bin\mariadb-dump.exe",
            @"C:\Program Files (x86)\MariaDB 10.11\bin\mariadb-dump.exe"
        };

        private static readonly string[] MariaDbClientWindowsPaths =
        {
            @"C:\Program Files\MariaDB 11.4\bin\mariadb.exe",
            @"C:\Program Files\MariaDB 11.0\bin\mariadb.exe",
            @"C:\Program Files\MariaDB 10.11\bin\mariadb.exe",
            @"C:\Program Files\MariaDB 10.6\bin\mariadb.exe",
            @"C:\Program Files (x86)\MariaDB 11.4\bin\mariadb.exe",
            @"C:\Program Files (x86)\MariaDB 10.11\bin\mariadb.exe"
        };

        // Rutas comunes de instalación de MySQL en Windows: se usan como último recurso cuando no se
        // encuentra mariadb-dump/mariadb, ya que muchas instalaciones de MariaDB solo traen (o
        // también aceptan) los binarios con nombre "mysql".
        private static readonly string[] MySqlDumpWindowsPaths =
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\Program Files\MySQL\MySQL Server 9.0\bin\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysqldump.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.4\bin\mysqldump.exe",
            @"C:\xampp\mysql\bin\mysqldump.exe"
        };

        private static readonly string[] MySqlClientWindowsPaths =
        {
            @"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
            @"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysql.exe",
            @"C:\Program Files\MySQL\MySQL Server 9.0\bin\mysql.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysql.exe",
            @"C:\Program Files (x86)\MySQL\MySQL Server 8.4\bin\mysql.exe",
            @"C:\xampp\mysql\bin\mysql.exe"
        };

        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<MariaDbBackupProvider> _logger;

        public string DatabaseType => "MariaDB";

        public MariaDbBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<MariaDbBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        /// <summary>
        /// Busca mariadb-dump (nombre moderno de la herramienta de volcado de MariaDB) y, si no se
        /// encuentra, recurre a mysqldump: las instalaciones de MariaDB suelen incluir ambos nombres,
        /// o solo el de MySQL en versiones/paquetes más antiguos.
        /// </summary>
        private static string ResolveDumpTool()
        {
            string path = ExecutableLocator.Find("mariadb-dump", MariaDbDumpWindowsPaths, "mariadb-dump");
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return ExecutableLocator.Find("mysqldump", MySqlDumpWindowsPaths, "mysqldump");
        }

        /// <summary>
        /// Busca el cliente mariadb y, si no se encuentra, recurre al cliente mysql (mismo motivo que
        /// <see cref="ResolveDumpTool"/>).
        /// </summary>
        private static string ResolveClientTool()
        {
            string path = ExecutableLocator.Find("mariadb", MariaDbClientWindowsPaths, "mariadb");
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            return ExecutableLocator.Find("mysql", MySqlClientWindowsPaths, "mysql");
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                var mariadbConfig = await _context.MariaDbDataBases.FindAsync(databaseId);
                if (mariadbConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de MariaDB no encontrada");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{mariadbConfig.DatabaseName}_backup_{timestamp}.sql";

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

                tempBackupPath = Path.Combine(tempFolder, fileName);

                string mariadbDumpPath = ResolveDumpTool();
                if (string.IsNullOrEmpty(mariadbDumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró mariadb-dump ni mysqldump. Asegúrate de que MariaDB esté instalado y mariadb-dump (o mysqldump) esté en el PATH del sistema.");
                }

                // La contraseña se pasa a mariadb-dump/mysqldump mediante un archivo de opciones
                // temporal (--defaults-extra-file) en vez de --password en la línea de comandos, para
                // que no quede expuesta en la lista de procesos del sistema (ps/Task Manager).
                string plainPassword = _credentialProtector.Unprotect(mariadbConfig.Password) ?? string.Empty;
                string credentialsFilePath = Path.Combine(Path.GetTempPath(), $"mariadbdump_{Guid.NewGuid():N}.cnf");
                await File.WriteAllTextAsync(credentialsFilePath,
                    $"[client]\nuser={mariadbConfig.Username}\npassword={plainPassword}\n");

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                try
                {
                    // --defaults-extra-file debe ser el primer argumento para que mariadb-dump/mysqldump lo reconozca
                    string arguments = $"--defaults-extra-file=\"{credentialsFilePath}\" " +
                                     $"--host={mariadbConfig.Host} " +
                                     $"--port={mariadbConfig.Port} " +
                                     $"--databases {mariadbConfig.DatabaseName} " +
                                     $"--result-file=\"{tempBackupPath}\" " +
                                     "--single-transaction " +
                                     "--routines " +
                                     "--triggers";

                    var processStartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = mariadbDumpPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var process = System.Diagnostics.Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso mariadb-dump");
                        }

                        bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                        if (!exited)
                        {
                            process.Kill();
                            return (false, null, string.Empty, string.Empty, "El proceso mariadb-dump excedió el tiempo límite de 5 minutos");
                        }

                        string errorOutput = await process.StandardError.ReadToEndAsync();

                        if (process.ExitCode != 0)
                        {
                            return (false, null, string.Empty, string.Empty, $"Error al ejecutar mariadb-dump: {errorOutput}");
                        }
                    }
                }
                finally
                {
                    try
                    {
                        File.Delete(credentialsFilePath);
                    }
                    catch { /* Ignorar errores al eliminar el archivo de credenciales temporal */ }
                }

                if (!File.Exists(tempBackupPath))
                {
                    return (false, null, string.Empty, string.Empty, "El archivo de backup no se creó correctamente");
                }

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempBackupPath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                memoryStream.Position = 0;
                TryDeleteFile(tempBackupPath);

                return (true, memoryStream, fileName, mariadbConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempBackupPath);
                _logger.LogError(ex, "Error al crear backup de MariaDB {DatabaseId}", databaseId);
                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { /* Ignorar errores al eliminar archivo temporal */ }
        }

        public async Task<(bool success, string message)> RestoreBackupAsync(int databaseId, MemoryStream backupZipStream)
        {
            string? credentialsFilePath = null;

            try
            {
                var mariadbConfig = await _context.MariaDbDataBases.FindAsync(databaseId);
                if (mariadbConfig == null)
                {
                    return (false, "Configuración de MariaDB no encontrada");
                }

                byte[] dumpBytes = BackupZipHelper.ExtractSingleEntry(backupZipStream);

                string mariadbPath = ResolveClientTool();
                if (string.IsNullOrEmpty(mariadbPath))
                {
                    return (false, "No se encontró el cliente mariadb ni mysql. Asegúrate de que MariaDB esté instalado y mariadb (o mysql) esté en el PATH del sistema.");
                }

                string plainPassword = _credentialProtector.Unprotect(mariadbConfig.Password) ?? string.Empty;
                credentialsFilePath = Path.Combine(Path.GetTempPath(), $"mariadb_restore_{Guid.NewGuid():N}.cnf");
                await File.WriteAllTextAsync(credentialsFilePath, $"[client]\nuser={mariadbConfig.Username}\npassword={plainPassword}\n");

                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(credentialsFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                string arguments = $"--defaults-extra-file=\"{credentialsFilePath}\" " +
                                    $"--host={mariadbConfig.Host} " +
                                    $"--port={mariadbConfig.Port} " +
                                    $"{mariadbConfig.DatabaseName}";

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mariadbPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    return (false, "No se pudo iniciar el proceso mariadb");
                }

                // El dump se alimenta por stdin, igual que "mariadb db < dump.sql" en una terminal.
                await process.StandardInput.BaseStream.WriteAsync(dumpBytes);
                process.StandardInput.Close();

                bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                if (!exited)
                {
                    process.Kill();
                    return (false, "El proceso mariadb excedió el tiempo límite de 5 minutos");
                }

                string errorOutput = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                {
                    return (false, $"Error al restaurar con mariadb: {errorOutput}");
                }

                return (true, $"Base de datos '{mariadbConfig.DatabaseName}' restaurada exitosamente desde el backup.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restaurar backup de MariaDB {DatabaseId}", databaseId);
                return (false, $"Error al restaurar backup: {ex.Message}");
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
        }
    }
}
