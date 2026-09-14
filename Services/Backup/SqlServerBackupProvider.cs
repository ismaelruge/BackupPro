using System.Security.AccessControl;
using BackupPro.Data;
using BackupPro.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Genera un backup de una base de datos SQL Server configurada.</summary>
    public class SqlServerBackupProvider : IDatabaseBackupProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<SqlServerBackupProvider> _logger;

        public string DatabaseType => "SqlServer";

        public SqlServerBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<SqlServerBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        /// <summary>
        /// Otorga permiso de escritura en la carpeta temporal de backups únicamente a las cuentas de
        /// servicio de SQL Server que realmente necesitan escribir el archivo .bak (el motor de SQL
        /// Server, no el proceso de esta aplicación, es quien escribe ese archivo). No se otorgan
        /// permisos a "Everyone" ni a "BUILTIN\Users": ampliar el acceso a cualquier usuario local de
        /// la máquina a una carpeta que contiene backups completos de bases de datos es un riesgo
        /// innecesario.
        /// </summary>
        private static bool EnsureFolderWritePermissions(string folderPath)
        {
            try
            {
                var directoryInfo = new DirectoryInfo(folderPath);
                var directorySecurity = directoryInfo.GetAccessControl();

                string[] accounts =
                {
                    @"NT Service\MSSQLSERVER",
                    @"NT Service\SQLEXPRESS",
                    @"NT AUTHORITY\NETWORK SERVICE",
                    @"NT AUTHORITY\SYSTEM"
                };

                foreach (var account in accounts)
                {
                    try
                    {
                        var fileSystemRule = new FileSystemAccessRule(
                            account,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow);

                        directorySecurity.AddAccessRule(fileSystemRule);
                    }
                    catch
                    {
                        // Ignorar si la cuenta no existe
                    }
                }

                directorySecurity.SetAccessRuleProtection(false, true);
                directoryInfo.SetAccessControl(directorySecurity);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                var sqlServerConfig = await _context.SqlServerDataBases.FindAsync(databaseId);
                if (sqlServerConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de SQL Server no encontrada");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{sqlServerConfig.DatabaseName}_backup_{timestamp}.bak";

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

                EnsureFolderWritePermissions(tempFolder);

                tempBackupPath = Path.Combine(tempFolder, fileName);

                string connectionString;
                if (sqlServerConfig.IntegratedSecurity)
                {
                    connectionString = $"Server={sqlServerConfig.Host},{sqlServerConfig.Port};Database={sqlServerConfig.DatabaseName};Integrated Security=True;TrustServerCertificate={sqlServerConfig.TrustServerCertificate};";
                }
                else
                {
                    string plainPassword = _credentialProtector.Unprotect(sqlServerConfig.Password) ?? string.Empty;
                    connectionString = $"Server={sqlServerConfig.Host},{sqlServerConfig.Port};Database={sqlServerConfig.DatabaseName};User Id={sqlServerConfig.Username};Password={plainPassword};TrustServerCertificate={sqlServerConfig.TrustServerCertificate};";
                }

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    // El nombre de la base de datos es un identificador, no puede parametrizarse con
                    // @param; se escapa duplicando los corchetes de cierre.
                    string escapedDatabaseName = sqlServerConfig.DatabaseName.Replace("]", "]]");
                    string backupCommand = $@"
                        BACKUP DATABASE [{escapedDatabaseName}]
                        TO DISK = @backupPath
                        WITH FORMAT,
                             INIT,
                             NAME = @backupName,
                             STATS = 10";

                    using (var command = new SqlCommand(backupCommand, connection))
                    {
                        command.CommandTimeout = 300; // 5 minutos de timeout
                        command.Parameters.AddWithValue("@backupPath", tempBackupPath);
                        command.Parameters.AddWithValue("@backupName", $"{sqlServerConfig.DatabaseName} Backup {timestamp}");

                        await command.ExecuteNonQueryAsync();
                    }
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

                return (true, memoryStream, fileName, sqlServerConfig.DatabaseName, string.Empty);
            }
            catch (SqlException sqlEx)
            {
                _logger.LogError(sqlEx, "Error de SQL Server al crear backup de {DatabaseId}", databaseId);
                TryDeleteFile(tempBackupPath);
                return (false, null, string.Empty, string.Empty, $"Error de SQL Server: {sqlEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear backup de SQL Server {DatabaseId}", databaseId);
                TryDeleteFile(tempBackupPath);
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
    }
}
