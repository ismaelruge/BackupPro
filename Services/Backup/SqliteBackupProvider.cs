using BackupPro.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Genera el backup de un archivo de base de datos SQLite configurado, usando la API nativa
    /// <see cref="SqliteConnection.BackupDatabase(SqliteConnection)"/> (equivalente al comando
    /// ".backup" de la herramienta sqlite3): no requiere ninguna herramienta externa, a diferencia
    /// de los demás motores soportados.
    /// </summary>
    public class SqliteBackupProvider : IDatabaseBackupProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SqliteBackupProvider> _logger;

        public string DatabaseType => "SQLite";

        public SqliteBackupProvider(ApplicationDbContext context, ILogger<SqliteBackupProvider> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId)
        {
            string tempBackupPath = string.Empty;

            try
            {
                var sqliteConfig = await _context.SqliteDataBases.FindAsync(databaseId);
                if (sqliteConfig == null)
                {
                    return (false, null, string.Empty, string.Empty, "Configuración de SQLite no encontrada");
                }

                if (!File.Exists(sqliteConfig.FilePath))
                {
                    return (false, null, string.Empty, string.Empty, $"No se encontró el archivo SQLite en la ruta: {sqliteConfig.FilePath}");
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"{sqliteConfig.ConfigurationName}_backup_{timestamp}.db";

                tempBackupPath = Path.Combine(Path.GetTempPath(), $"sqlite_backup_{Guid.NewGuid():N}.db");

                using (var source = new SqliteConnection($"Data Source={sqliteConfig.FilePath};Mode=ReadOnly"))
                using (var destination = new SqliteConnection($"Data Source={tempBackupPath}"))
                {
                    await source.OpenAsync();
                    await destination.OpenAsync();

                    // API nativa e in-process de SQLite para copiar toda la base de datos de forma
                    // consistente, sin necesidad de bloquear el archivo ni de ninguna herramienta CLI.
                    source.BackupDatabase(destination);
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
                TryDeleteTempFile(tempBackupPath);

                return (true, memoryStream, fileName, sqliteConfig.ConfigurationName, string.Empty);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempBackupPath);
                _logger.LogError(ex, "Error al crear backup de SQLite {DatabaseId}", databaseId);
                return (false, null, string.Empty, string.Empty, $"Error al crear backup: {ex.Message}");
            }
        }

        private static void TryDeleteTempFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch { /* Ignorar errores al eliminar el archivo temporal */ }
        }

        public async Task<(bool success, string message)> RestoreBackupAsync(int databaseId, MemoryStream backupZipStream)
        {
            string tempRestorePath = string.Empty;

            try
            {
                var sqliteConfig = await _context.SqliteDataBases.FindAsync(databaseId);
                if (sqliteConfig == null)
                {
                    return (false, "Configuración de SQLite no encontrada");
                }

                // El .zip que sube el IStorageProvider contiene, como única entrada, el archivo .db
                // que generó CreateBackupAsync.
                byte[] dbBytes = BackupZipHelper.ExtractSingleEntry(backupZipStream);

                tempRestorePath = Path.Combine(Path.GetTempPath(), $"sqlite_restore_{Guid.NewGuid():N}.db");
                await File.WriteAllBytesAsync(tempRestorePath, dbBytes);

                // Verificar que el archivo restaurado sea una base de datos SQLite válida antes de
                // reemplazar el archivo en producción.
                using (var validation = new SqliteConnection($"Data Source={tempRestorePath};Mode=ReadOnly"))
                {
                    await validation.OpenAsync();
                    using var command = validation.CreateCommand();
                    command.CommandText = "PRAGMA schema_version;";
                    await command.ExecuteScalarAsync();
                }

                // SqliteConnection.ClearAllPools libera cualquier handle nativo que SQLite pueda
                // mantener abierto sobre el archivo destino antes de reemplazarlo.
                SqliteConnection.ClearAllPools();

                string? targetDirectory = Path.GetDirectoryName(sqliteConfig.FilePath);
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                File.Copy(tempRestorePath, sqliteConfig.FilePath, overwrite: true);

                return (true, $"Base de datos '{sqliteConfig.ConfigurationName}' restaurada exitosamente desde el backup.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restaurar backup de SQLite {DatabaseId}", databaseId);
                return (false, $"Error al restaurar backup: {ex.Message}");
            }
            finally
            {
                TryDeleteTempFile(tempRestorePath);
            }
        }
    }
}
