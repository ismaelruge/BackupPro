using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    /// <summary>
    /// Cubre lo que se puede probar sin una instancia real de Oracle: la configuración no encontrada,
    /// la limitación "sin acceso local al DIRECTORY" (Data Pump es del lado del servidor por diseño,
    /// ver comentario de <see cref="OracleBackupProvider"/>), y la ruta de "herramienta no encontrada"
    /// — expdp/impdp no están instalados en este entorno de pruebas, así que ese camino se ejercita de
    /// forma natural en cuanto se configura una carpeta local de DATA_PUMP_DIR.
    /// </summary>
    public class OracleBackupProviderTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static CredentialProtector CreateCredentialProtector()
        {
            // Clave maestra fija de 32 bytes (AES-256) para que las pruebas sean deterministas y no
            // dependan de un archivo Data/keystore.db generado en disco (mismo patrón que
            // CredentialProtectorTests).
            var masterKey = Convert.ToBase64String(new byte[32]);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:MasterKey"] = masterKey
                })
                .Build();

            return new CredentialProtector(configuration, NullLogger<CredentialProtector>.Instance);
        }

        private static OracleDataBase CreateConfig(int id = 1, string? dumpDirectoryPath = null, string host = "oracle.empresa.com") => new()
        {
            Id = id,
            ConfigurationName = "Oracle Prueba",
            Host = host,
            Port = 1521,
            ServiceName = "ORCLPDB1",
            Username = "backup_user",
            Password = "clave-en-texto-plano",
            DumpDirectoryPath = dumpDirectoryPath
        };

        [Fact]
        public void DatabaseType_IsOracle()
        {
            using var context = CreateContext();
            var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

            Assert.Equal("Oracle", provider.DatabaseType);
        }

        [Fact]
        public async Task CreateBackupAsync_ConfigNotFound_ReturnsFailureWithoutThrowing()
        {
            using var context = CreateContext();
            var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

            var (success, backupStream, fileName, databaseName, errorMessage) = await provider.CreateBackupAsync(999);

            Assert.False(success);
            Assert.Null(backupStream);
            Assert.Equal(string.Empty, fileName);
            Assert.Equal(string.Empty, databaseName);
            Assert.Contains("no encontrada", errorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RestoreBackupAsync_ConfigNotFound_ReturnsFailureWithoutThrowing()
        {
            using var context = CreateContext();
            var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

            var (success, message) = await provider.RestoreBackupAsync(999, new MemoryStream());

            Assert.False(success);
            Assert.Contains("no encontrada", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateBackupAsync_NoDumpDirectoryConfigured_ReturnsDirectoryAccessLimitationMessage()
        {
            using var context = CreateContext();
            context.OracleDataBases.Add(CreateConfig(dumpDirectoryPath: null));
            await context.SaveChangesAsync();

            var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

            var (success, backupStream, _, _, errorMessage) = await provider.CreateBackupAsync(1);

            Assert.False(success);
            Assert.Null(backupStream);
            Assert.Contains("DIRECTORY", errorMessage);
            Assert.Contains("Data Pump", errorMessage);
        }

        [Fact]
        public async Task CreateBackupAsync_RemoteHostEvenWithDumpDirectoryConfigured_ReturnsDirectoryAccessLimitationMessage()
        {
            // DumpDirectoryPath solo es utilizable cuando el servidor Oracle corre en esta misma
            // máquina (Host local): configurarlo mientras el host sigue siendo remoto no alcanza,
            // porque esa carpeta no tiene por qué corresponder al DIRECTORY del servidor remoto.
            using var context = CreateContext();
            string localDir = Directory.CreateTempSubdirectory("oracle_dumpdir_").FullName;
            try
            {
                context.OracleDataBases.Add(CreateConfig(host: "oracle-remoto.empresa.com", dumpDirectoryPath: localDir));
                await context.SaveChangesAsync();

                var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

                var (success, backupStream, _, _, errorMessage) = await provider.CreateBackupAsync(1);

                Assert.False(success);
                Assert.Null(backupStream);
                Assert.Contains("DIRECTORY", errorMessage);
            }
            finally
            {
                Directory.Delete(localDir, recursive: true);
            }
        }

        [Fact]
        public async Task CreateBackupAsync_LocalHostWithDumpDirectory_ButExpdpNotAvailable_FailsCleanlyWithoutThrowing()
        {
            // Host local + DumpDirectoryPath configurado y existente: la limitación de "sin acceso al
            // DIRECTORY" queda superada, así que el provider sigue adelante y busca expdp, que no está
            // instalado en este entorno de pruebas (no hay Oracle Instant Client).
            //
            // ExecutableLocator.Find solo devuelve cadena vacía (activando ToolNotFoundMessage) cuando
            // no encuentra el ejecutable en PATH NI en las rutas comunes de Windows Y el proceso corre
            // en Windows; en Linux/Mac, por diseño, siempre confía en el PATH del proceso hijo y
            // devuelve el nombre del comando tal cual (ver el comentario de ExecutableLocator) — mismo
            // comportamiento que ya tienen PostgresBackupProvider/MySqlBackupProvider. En este sandbox
            // Linux sin Oracle instalado, eso hace que Process.Start falle al no encontrar el binario;
            // lo importante y lo que se prueba acá es que esa falla se propaga como un resultado
            // (success:false) con un mensaje, nunca como una excepción sin manejar ni como un falso
            // "success:true".
            using var context = CreateContext();
            string localDir = Directory.CreateTempSubdirectory("oracle_dumpdir_").FullName;
            try
            {
                context.OracleDataBases.Add(CreateConfig(host: "localhost", dumpDirectoryPath: localDir));
                await context.SaveChangesAsync();

                var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

                var (success, backupStream, _, _, errorMessage) = await provider.CreateBackupAsync(1);

                Assert.False(success);
                Assert.Null(backupStream);
                Assert.False(string.IsNullOrWhiteSpace(errorMessage));
                Assert.Contains("expdp", errorMessage);
            }
            finally
            {
                Directory.Delete(localDir, recursive: true);
            }
        }

        [Fact]
        public async Task RestoreBackupAsync_NoDumpDirectoryConfigured_ReturnsDirectoryAccessLimitationMessage()
        {
            using var context = CreateContext();
            context.OracleDataBases.Add(CreateConfig(dumpDirectoryPath: null));
            await context.SaveChangesAsync();

            var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

            using var zipStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("backup.dmp");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write("contenido de prueba");
            }
            zipStream.Position = 0;

            var (success, message) = await provider.RestoreBackupAsync(1, zipStream);

            Assert.False(success);
            Assert.Contains("DIRECTORY", message);
        }

        [Fact]
        public async Task RestoreBackupAsync_LocalHostWithDumpDirectory_ButImpdpNotAvailable_FailsCleanlyWithoutThrowing()
        {
            // Ver el comentario de CreateBackupAsync_LocalHostWithDumpDirectory_ButExpdpNotAvailable_FailsCleanlyWithoutThrowing:
            // en este sandbox Linux sin Oracle instalado, ExecutableLocator confía en el PATH y no
            // devuelve cadena vacía, así que el fallo surge de Process.Start (impdp inexistente) en
            // vez de ToolNotFoundMessage. Lo que importa es que nunca se reporta éxito falso.
            using var context = CreateContext();
            string localDir = Directory.CreateTempSubdirectory("oracle_dumpdir_").FullName;
            try
            {
                context.OracleDataBases.Add(CreateConfig(host: "127.0.0.1", dumpDirectoryPath: localDir));
                await context.SaveChangesAsync();

                var provider = new OracleBackupProvider(context, CreateCredentialProtector(), NullLogger<OracleBackupProvider>.Instance);

                using var zipStream = new MemoryStream();
                using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                {
                    var entry = archive.CreateEntry("backup.dmp");
                    using var entryStream = entry.Open();
                    using var writer = new StreamWriter(entryStream);
                    writer.Write("contenido de prueba");
                }
                zipStream.Position = 0;

                var (success, message) = await provider.RestoreBackupAsync(1, zipStream);

                Assert.False(success);
                Assert.False(string.IsNullOrWhiteSpace(message));
                Assert.Contains("impdp", message);
            }
            finally
            {
                Directory.Delete(localDir, recursive: true);
            }
        }
    }
}
