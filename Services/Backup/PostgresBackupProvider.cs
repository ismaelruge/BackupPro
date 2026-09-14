using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>Genera un backup de una base de datos PostgreSQL configurada usando pg_dump.</summary>
    public class PostgresBackupProvider : IDatabaseBackupProvider
    {
        private static readonly string[] WindowsCommonPaths =
        {
            @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\14\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\13\bin\pg_dump.exe",
            @"C:\Program Files\PostgreSQL\12\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\18\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\17\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\16\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\15\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\14\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\13\bin\pg_dump.exe",
            @"C:\Program Files (x86)\PostgreSQL\12\bin\pg_dump.exe"
        };

        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<PostgresBackupProvider> _logger;

        public string DatabaseType => "PostgreSQL";

        public PostgresBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<PostgresBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                var postgresConfig = await _context.PostgresSqlDataBases.FindAsync(databaseId);
                if (postgresConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de PostgreSQL no encontrada");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{postgresConfig.DatabaseName}_backup_{timestamp}.sql";

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

                string pgDumpPath = ExecutableLocator.Find("pg_dump", WindowsCommonPaths, "pg_dump");
                if (string.IsNullOrEmpty(pgDumpPath))
                {
                    return (false, null, string.Empty, string.Empty, "No se encontró pg_dump. Asegúrate de que PostgreSQL esté instalado y pg_dump esté en el PATH del sistema.");
                }

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // pg_dump lee la contraseña de PGPASSWORD; nunca se pasa por línea de comandos para
                // no exponerla en la lista de procesos.
                processStartInfo.EnvironmentVariables["PGPASSWORD"] = _credentialProtector.Unprotect(postgresConfig.Password) ?? string.Empty;

                string arguments = $"--host={postgresConfig.Host} " +
                                 $"--port={postgresConfig.Port} " +
                                 $"--username={postgresConfig.Username} " +
                                 $"--dbname={postgresConfig.DatabaseName} " +
                                 $"--file=\"{tempBackupPath}\" " +
                                 "--format=plain " +
                                 "--no-owner " +
                                 "--no-acl " +
                                 "--clean " +
                                 "--if-exists";

                processStartInfo.Arguments = arguments;

                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso pg_dump");
                    }

                    bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                    if (!exited)
                    {
                        process.Kill();
                        return (false, null, string.Empty, string.Empty, "El proceso pg_dump excedió el tiempo límite de 5 minutos");
                    }

                    string errorOutput = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        return (false, null, string.Empty, string.Empty, $"Error al ejecutar pg_dump: {errorOutput}");
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

                return (true, memoryStream, fileName, postgresConfig.DatabaseName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteFile(tempBackupPath);
                _logger.LogError(ex, "Error al crear backup de PostgreSQL {DatabaseId}", databaseId);
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
