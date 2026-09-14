using BackupPro.Data;
using BackupPro.Services;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Genera/restaura el backup de una base de datos Oracle configurada usando Oracle Data Pump
    /// (<c>expdp</c>/<c>impdp</c>).
    ///
    /// Esta es, por diseño, la integración más limitada de las cuatro soportadas por BackupPro.
    /// A diferencia de mysqldump/pg_dump/mongodump (herramientas puramente del lado CLIENTE, que
    /// escriben el dump en la máquina donde se ejecutan), Data Pump es una utilidad del lado del
    /// SERVIDOR: expdp/impdp le piden a la instancia de Oracle que lea/escriba el archivo .dmp a
    /// través de un objeto DIRECTORY (aquí se usa <c>DATA_PUMP_DIR</c>, el directorio que Oracle crea
    /// por defecto en toda base de datos), sobre el sistema de archivos del SERVIDOR Oracle — nunca
    /// sobre la máquina donde corre BackupPro. Ni siquiera es necesario que expdp/impdp se ejecuten en
    /// el servidor: pueden ejecutarse remotamente (requieren un Oracle Instant Client con el paquete
    /// Tools, o el cliente completo de Oracle, instalado en la máquina que los invoca), pero el
    /// archivo .dmp igual queda escrito del lado del servidor.
    ///
    /// Por lo tanto, esta integración solo puede completar un backup/restore de punta a punta cuando
    /// el servidor Oracle corre en el mismo host que esta app (Host = localhost/127.0.0.1) Y se
    /// configura <see cref="Models.OracleDataBase.DumpDirectoryPath"/> con la ruta local equivalente
    /// al DIRECTORY DATA_PUMP_DIR del servidor. En cualquier otro caso (servidor remoto, o
    /// DumpDirectoryPath sin configurar) no hay forma honesta de recuperar/enviar el .dmp sin asumir
    /// cosas sobre la infraestructura de cada cliente (recurso de red compartido, FTP al servidor,
    /// etc.), así que se devuelve un error explicando la limitación en vez de fingir éxito.
    /// </summary>
    public class OracleBackupProvider : IDatabaseBackupProvider
    {
        private static readonly string[] ExpdpWindowsCommonPaths =
        {
            @"C:\oracle\product\21.0.0\dbhome_1\bin\expdp.exe",
            @"C:\oracle\product\19.0.0\dbhome_1\bin\expdp.exe",
            @"C:\oracle\instantclient_21_x\expdp.exe",
            @"C:\oracle\instantclient_19_x\expdp.exe",
            @"C:\app\oracle\product\21.0.0\dbhome_1\bin\expdp.exe",
            @"C:\app\oracle\product\19.0.0\dbhome_1\bin\expdp.exe"
        };

        private static readonly string[] ImpdpWindowsCommonPaths =
        {
            @"C:\oracle\product\21.0.0\dbhome_1\bin\impdp.exe",
            @"C:\oracle\product\19.0.0\dbhome_1\bin\impdp.exe",
            @"C:\oracle\instantclient_21_x\impdp.exe",
            @"C:\oracle\instantclient_19_x\impdp.exe",
            @"C:\app\oracle\product\21.0.0\dbhome_1\bin\impdp.exe",
            @"C:\app\oracle\product\19.0.0\dbhome_1\bin\impdp.exe"
        };

        private const string ToolNotFoundMessage =
            "No se encontró expdp/impdp. Asegúrate de tener instalado Oracle Instant Client (paquete Tools) " +
            "o el cliente completo de Oracle, y que expdp/impdp estén en el PATH del sistema.";

        private const string DirectoryAccessLimitationMessage =
            "Oracle Data Pump requiere acceso al sistema de archivos del servidor Oracle (vía el objeto " +
            "DIRECTORY) para poder recuperar el archivo .dmp generado; esta integración no incluye ese paso " +
            "porque depende de cómo esté configurado cada servidor Oracle (recurso de red compartido, mismo " +
            "host que la app, etc.). Si el servidor Oracle corre en esta misma máquina, configura 'Carpeta " +
            "local de DATA_PUMP_DIR' en la configuración de esta base de datos con la ruta local equivalente " +
            "al objeto DIRECTORY DATA_PUMP_DIR del servidor para poder completar el backup/restore.";

        private readonly ApplicationDbContext _context;
        private readonly CredentialProtector _credentialProtector;
        private readonly ILogger<OracleBackupProvider> _logger;

        public string DatabaseType => "Oracle";

        public OracleBackupProvider(ApplicationDbContext context, CredentialProtector credentialProtector, ILogger<OracleBackupProvider> logger)
        {
            _context = context;
            _credentialProtector = credentialProtector;
            _logger = logger;
        }

        /// <summary>
        /// True cuando <paramref name="host"/> apunta a esta misma máquina, único caso en el que
        /// <see cref="Models.OracleDataBase.DumpDirectoryPath"/> puede corresponder a una carpeta
        /// visible directamente en el disco local.
        /// </summary>
        private static bool IsLocalHost(string host)
        {
            string trimmed = (host ?? string.Empty).Trim();
            return trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("127.0.0.1", StringComparison.Ordinal)
                || trimmed.Equals("::1", StringComparison.Ordinal);
        }

        /// <summary>
        /// True cuando hay una carpeta local configurada y existente que se puede usar como
        /// equivalente del DIRECTORY DATA_PUMP_DIR del servidor (ver comentario de la clase).
        /// </summary>
        private static bool CanAccessDumpDirectoryLocally(Models.OracleDataBase config) =>
            IsLocalHost(config.Host)
            && !string.IsNullOrWhiteSpace(config.DumpDirectoryPath)
            && Directory.Exists(config.DumpDirectoryPath);

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string localDumpPath = string.Empty;
            string localLogPath = string.Empty;

            try
            {
                var oracleConfig = await _context.OracleDataBases.FindAsync(databaseId);
                if (oracleConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de Oracle no encontrada");
                }

                // Ver el comentario de la clase: sin acceso al filesystem del servidor Oracle (vía
                // DIRECTORY) no hay forma honesta de recuperar el .dmp que expdp generaría del otro
                // lado. Se comprueba esto ANTES de buscar expdp: no tiene sentido exigir el cliente de
                // Oracle instalado si de todas formas no vamos a poder completar la operación.
                if (!CanAccessDumpDirectoryLocally(oracleConfig))
                {
                    return (false, null, string.Empty, string.Empty, DirectoryAccessLimitationMessage);
                }

                string expdpPath = ExecutableLocator.Find("expdp", ExpdpWindowsCommonPaths, "expdp");
                if (string.IsNullOrEmpty(expdpPath))
                {
                    return (false, null, string.Empty, string.Empty, ToolNotFoundMessage);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string dumpFileName = $"{oracleConfig.ServiceName}_backup_{timestamp}.dmp";
                string logFileName = $"{oracleConfig.ServiceName}_backup_{timestamp}.log";
                string fileName = dumpFileName;

                localDumpPath = Path.Combine(oracleConfig.DumpDirectoryPath!, dumpFileName);
                localLogPath = Path.Combine(oracleConfig.DumpDirectoryPath!, logFileName);

                string plainPassword = _credentialProtector.Unprotect(oracleConfig.Password) ?? string.Empty;

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = expdpPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Nota de seguridad: a diferencia de pg_dump (PGPASSWORD) o mysqldump
                // (--defaults-extra-file), expdp/impdp no tienen forma de recibir la contraseña fuera
                // de la línea de comandos EZCONNECT (usuario/contraseña@host:puerto/servicio) — queda
                // visible en la lista de procesos del sistema mientras dura la operación. Es una
                // limitación de la propia herramienta de Oracle, no de esta integración.
                processStartInfo.ArgumentList.Add($"{oracleConfig.Username}/{plainPassword}@{oracleConfig.Host}:{oracleConfig.Port}/{oracleConfig.ServiceName}");
                processStartInfo.ArgumentList.Add("directory=DATA_PUMP_DIR");
                processStartInfo.ArgumentList.Add($"dumpfile={dumpFileName}");
                processStartInfo.ArgumentList.Add($"logfile={logFileName}");
                processStartInfo.ArgumentList.Add($"schemas={oracleConfig.Username}");

                using (var process = System.Diagnostics.Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return (false, null, string.Empty, string.Empty, "No se pudo iniciar el proceso expdp");
                    }

                    bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                    if (!exited)
                    {
                        process.Kill();
                        return (false, null, string.Empty, string.Empty, "El proceso expdp excedió el tiempo límite de 5 minutos");
                    }

                    string standardOutput = await process.StandardOutput.ReadToEndAsync();
                    string errorOutput = await process.StandardError.ReadToEndAsync();

                    if (process.ExitCode != 0)
                    {
                        string details = string.IsNullOrWhiteSpace(errorOutput) ? standardOutput : errorOutput;
                        return (false, null, string.Empty, string.Empty, $"Error al ejecutar expdp: {details}");
                    }
                }

                // expdp escribió el .dmp en el DIRECTORY DATA_PUMP_DIR del lado del SERVIDOR Oracle;
                // solo llegamos hasta acá porque CanAccessDumpDirectoryLocally confirmó que ese
                // DIRECTORY corresponde a una carpeta visible en este mismo disco.
                if (!File.Exists(localDumpPath))
                {
                    return (false, null, string.Empty, string.Empty,
                        $"expdp finalizó pero no se encontró el archivo '{dumpFileName}' en '{oracleConfig.DumpDirectoryPath}'. " +
                        "Verifica que esa carpeta sea realmente la ruta local del objeto DIRECTORY DATA_PUMP_DIR del servidor Oracle.");
                }

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(localDumpPath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(memoryStream);
                }

                memoryStream.Position = 0;
                TryDeleteFile(localDumpPath);
                TryDeleteFile(localLogPath);

                return (true, memoryStream, fileName, oracleConfig.ServiceName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteFile(localDumpPath);
                TryDeleteFile(localLogPath);
                _logger.LogError(ex, "Error al crear backup de Oracle {DatabaseId}", databaseId);
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
            string localDumpPath = string.Empty;
            string localLogPath = string.Empty;

            try
            {
                var oracleConfig = await _context.OracleDataBases.FindAsync(databaseId);
                if (oracleConfig == null)
                {
                    return (false, "Configuración de Oracle no encontrada");
                }

                // Misma limitación que en CreateBackupAsync (ver comentario de la clase), aplicada en
                // sentido inverso: sin la carpeta local equivalente al DIRECTORY DATA_PUMP_DIR del
                // servidor no hay forma de ENTREGARLE el .dmp a impdp para que lo importe.
                if (!CanAccessDumpDirectoryLocally(oracleConfig))
                {
                    return (false, DirectoryAccessLimitationMessage);
                }

                byte[] dumpBytes = BackupZipHelper.ExtractSingleEntry(backupZipStream);

                string impdpPath = ExecutableLocator.Find("impdp", ImpdpWindowsCommonPaths, "impdp");
                if (string.IsNullOrEmpty(impdpPath))
                {
                    return (false, ToolNotFoundMessage);
                }

                string uniqueId = Guid.NewGuid().ToString("N");
                string dumpFileName = $"{oracleConfig.ServiceName}_restore_{uniqueId}.dmp";
                string logFileName = $"{oracleConfig.ServiceName}_restore_{uniqueId}.log";

                localDumpPath = Path.Combine(oracleConfig.DumpDirectoryPath!, dumpFileName);
                localLogPath = Path.Combine(oracleConfig.DumpDirectoryPath!, logFileName);

                // impdp lo va a leer del lado del servidor a través del DIRECTORY DATA_PUMP_DIR; como
                // ese DIRECTORY corresponde a esta misma carpeta local (confirmado arriba), basta con
                // escribir el .dmp acá para que quede disponible para el import.
                await File.WriteAllBytesAsync(localDumpPath, dumpBytes);

                string plainPassword = _credentialProtector.Unprotect(oracleConfig.Password) ?? string.Empty;

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = impdpPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                processStartInfo.ArgumentList.Add($"{oracleConfig.Username}/{plainPassword}@{oracleConfig.Host}:{oracleConfig.Port}/{oracleConfig.ServiceName}");
                processStartInfo.ArgumentList.Add("directory=DATA_PUMP_DIR");
                processStartInfo.ArgumentList.Add($"dumpfile={dumpFileName}");
                processStartInfo.ArgumentList.Add($"logfile={logFileName}");
                processStartInfo.ArgumentList.Add($"schemas={oracleConfig.Username}");
                // Operación destructiva por contrato de IDatabaseBackupProvider: reemplaza los datos
                // actuales por los del backup, incluyendo los objetos que ya existan en el schema.
                processStartInfo.ArgumentList.Add("table_exists_action=replace");

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    return (false, "No se pudo iniciar el proceso impdp");
                }

                bool exited = await Task.Run(() => process.WaitForExit(300000)); // 5 minutos

                if (!exited)
                {
                    process.Kill();
                    return (false, "El proceso impdp excedió el tiempo límite de 5 minutos");
                }

                string standardOutput = await process.StandardOutput.ReadToEndAsync();
                string errorOutput = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                {
                    string details = string.IsNullOrWhiteSpace(errorOutput) ? standardOutput : errorOutput;
                    return (false, $"Error al restaurar con impdp: {details}");
                }

                return (true, $"Base de datos '{oracleConfig.ServiceName}' restaurada exitosamente desde el backup.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restaurar backup de Oracle {DatabaseId}", databaseId);
                return (false, $"Error al restaurar backup: {ex.Message}");
            }
            finally
            {
                TryDeleteFile(localDumpPath);
                TryDeleteFile(localLogPath);
            }
        }
    }
}
