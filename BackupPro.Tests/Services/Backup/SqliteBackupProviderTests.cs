using System.IO.Compression;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services.Backup;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class SqliteBackupProviderTests : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private string CreateTempSqliteFile()
        {
            string path = Path.Combine(Path.GetTempPath(), $"sqlite_test_{Guid.NewGuid():N}.db");
            _tempFiles.Add(path);
            return path;
        }

        /// <summary>Crea un archivo SQLite real en disco con una tabla y una fila de datos.</summary>
        private static void CreateRealSqliteDatabase(string path, string value)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();

            using (var createCommand = connection.CreateCommand())
            {
                createCommand.CommandText = "CREATE TABLE Items (Id INTEGER PRIMARY KEY, Value TEXT NOT NULL);";
                createCommand.ExecuteNonQuery();
            }

            using (var insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = "INSERT INTO Items (Value) VALUES ($value);";
                insertCommand.Parameters.AddWithValue("$value", value);
                insertCommand.ExecuteNonQuery();
            }
        }

        private static string ReadFirstValue(string path)
        {
            using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM Items ORDER BY Id LIMIT 1;";
            return (string)command.ExecuteScalar()!;
        }

        /// <summary>
        /// Simula el .zip de una sola entrada que produce un <see cref="Services.IStorageProvider"/>
        /// al guardar el backup — el mismo formato que <see cref="BackupZipHelper"/> espera al
        /// restaurar.
        /// </summary>
        private static MemoryStream WrapInSingleEntryZip(byte[] content, string entryName)
        {
            var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }

            zipStream.Position = 0;
            return zipStream;
        }

        [Fact]
        public void DatabaseType_IsSqlite()
        {
            using var context = CreateContext();
            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            Assert.Equal("SQLite", provider.DatabaseType);
        }

        [Fact]
        public async Task CreateBackupAsync_UnknownConfiguration_ReturnsFailure()
        {
            using var context = CreateContext();
            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            var (success, stream, fileName, databaseName, errorMessage) = await provider.CreateBackupAsync(999);

            Assert.False(success);
            Assert.Null(stream);
            Assert.Contains("no encontrada", errorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateBackupAsync_MissingFile_ReturnsFailure()
        {
            using var context = CreateContext();
            var config = new SqliteDataBase
            {
                ConfigurationName = "Config sin archivo",
                FilePath = Path.Combine(Path.GetTempPath(), $"no_existe_{Guid.NewGuid():N}.db")
            };
            context.SqliteDataBases.Add(config);
            await context.SaveChangesAsync();

            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            var (success, stream, fileName, databaseName, errorMessage) = await provider.CreateBackupAsync(config.Id);

            Assert.False(success);
            Assert.Null(stream);
            Assert.Contains("No se encontró el archivo", errorMessage);
        }

        [Fact]
        public async Task CreateBackupAsync_RealDatabase_ProducesValidBackupWithSameData()
        {
            string sourcePath = CreateTempSqliteFile();
            CreateRealSqliteDatabase(sourcePath, "hola-mundo");

            using var context = CreateContext();
            var config = new SqliteDataBase
            {
                ConfigurationName = "Config real",
                FilePath = sourcePath
            };
            context.SqliteDataBases.Add(config);
            await context.SaveChangesAsync();

            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            var (success, stream, fileName, databaseName, errorMessage) = await provider.CreateBackupAsync(config.Id);

            Assert.True(success, errorMessage);
            Assert.NotNull(stream);
            Assert.StartsWith("Config real_backup_", fileName);
            Assert.EndsWith(".db", fileName);
            Assert.Equal("Config real", databaseName);
            Assert.True(stream!.Length > 0);

            // El backup devuelto debe ser un archivo .db válido con los mismos datos que el origen.
            string backupPath = CreateTempSqliteFile();
            await File.WriteAllBytesAsync(backupPath, stream.ToArray());

            Assert.Equal("hola-mundo", ReadFirstValue(backupPath));
        }

        [Fact]
        public async Task RestoreBackupAsync_UnknownConfiguration_ReturnsFailure()
        {
            using var context = CreateContext();
            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            using var zip = WrapInSingleEntryZip(new byte[] { 1, 2, 3 }, "backup.db");

            var (success, message) = await provider.RestoreBackupAsync(999, zip);

            Assert.False(success);
            Assert.Contains("no encontrada", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RestoreBackupAsync_RoundTrip_RestoresOriginalData()
        {
            string sourcePath = CreateTempSqliteFile();
            CreateRealSqliteDatabase(sourcePath, "valor-original");

            using var context = CreateContext();
            var config = new SqliteDataBase
            {
                ConfigurationName = "Config round-trip",
                FilePath = sourcePath
            };
            context.SqliteDataBases.Add(config);
            await context.SaveChangesAsync();

            var provider = new SqliteBackupProvider(context, NullLogger<SqliteBackupProvider>.Instance);

            // 1. Crear el backup mientras el archivo tiene el valor original.
            var (createSuccess, backupStream, _, _, createError) = await provider.CreateBackupAsync(config.Id);
            Assert.True(createSuccess, createError);
            Assert.NotNull(backupStream);

            using var zippedBackup = WrapInSingleEntryZip(backupStream!.ToArray(), "backup.db");

            // 2. Modificar ("corromper") el archivo origen para simular datos posteriores al backup.
            SqliteConnection.ClearAllPools();
            using (var connection = new SqliteConnection($"Data Source={sourcePath}"))
            {
                connection.Open();
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = "UPDATE Items SET Value = 'valor-modificado';";
                updateCommand.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            Assert.Equal("valor-modificado", ReadFirstValue(sourcePath));

            // 3. Restaurar desde el backup y verificar que el archivo vuelve a tener el dato original.
            var (restoreSuccess, restoreMessage) = await provider.RestoreBackupAsync(config.Id, zippedBackup);

            Assert.True(restoreSuccess, restoreMessage);
            Assert.Contains("restaurada exitosamente", restoreMessage);
            Assert.Equal("valor-original", ReadFirstValue(sourcePath));
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in _tempFiles)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch { /* Ignorar errores al limpiar archivos temporales de prueba */ }
            }
        }
    }
}
