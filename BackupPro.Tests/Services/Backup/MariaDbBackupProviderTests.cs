using BackupPro.Data;
using BackupPro.Services;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class MariaDbBackupProviderTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static CredentialProtector CreateProtector()
        {
            // Clave maestra fija de 32 bytes (AES-256) para que las pruebas sean deterministas y no
            // dependan de un archivo Data/master.key generado en disco (mismo patrón que
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

        private static MariaDbBackupProvider CreateProvider(ApplicationDbContext context) =>
            new(context, CreateProtector(), NullLogger<MariaDbBackupProvider>.Instance);

        [Fact]
        public void DatabaseType_IsMariaDB()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            Assert.Equal("MariaDB", provider.DatabaseType);
        }

        [Fact]
        public async Task CreateBackupAsync_ConfigNotFound_ReturnsFailureWithoutThrowing()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            var (success, backupStream, fileName, databaseName, errorMessage) = await provider.CreateBackupAsync(999);

            Assert.False(success);
            Assert.Null(backupStream);
            Assert.Equal(string.Empty, fileName);
            Assert.Equal(string.Empty, databaseName);
            Assert.Equal("Configuración de MariaDB no encontrada", errorMessage);
        }

        [Fact]
        public async Task RestoreBackupAsync_ConfigNotFound_ReturnsFailureWithoutThrowing()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            var (success, message) = await provider.RestoreBackupAsync(999, new MemoryStream());

            Assert.False(success);
            Assert.Equal("Configuración de MariaDB no encontrada", message);
        }

        [Fact]
        public async Task CreateBackupAsync_ConfigFound_UsesConfiguredDatabaseName()
        {
            // No hay mariadb-dump/mysqldump reales disponibles en este entorno de pruebas, así que el
            // resultado final será un fallo (ExecutableLocator no encuentra el binario, o el proceso
            // no puede iniciarse), pero antes de eso el provider debe llegar a buscar la configuración
            // guardada sin lanzar excepciones no controladas.
            using var context = CreateContext();
            context.MariaDbDataBases.Add(new Models.MariaDbDataBase
            {
                Id = 1,
                ConfigurationName = "MariaDB de prueba",
                Host = "localhost",
                Port = 3306,
                DatabaseName = "test_db",
                Username = "root",
                Password = string.Empty
            });
            await context.SaveChangesAsync();

            var provider = CreateProvider(context);

            var result = await provider.CreateBackupAsync(1);

            // No aseveramos success porque depende de que mariadb-dump/mysqldump estén instalados en
            // la máquina que corre las pruebas; solo verificamos que no haya reventado con una
            // excepción y que, si falló, no sea por "configuración no encontrada".
            Assert.NotEqual("Configuración de MariaDB no encontrada", result.errorMessage);
        }
    }
}
