using BackupPro.Data;
using BackupPro.Services;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class SftpStorageProviderTests
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
            // dependan de un archivo Data/master.key generado en disco.
            var masterKey = Convert.ToBase64String(new byte[32]);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Security:MasterKey"] = masterKey
                })
                .Build();

            return new CredentialProtector(configuration, NullLogger<CredentialProtector>.Instance);
        }

        private static SftpStorageProvider CreateProvider(ApplicationDbContext context) =>
            new(context, CreateProtector(), NullLogger<SftpStorageProvider>.Instance);

        [Fact]
        public void StorageType_IsSftp()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            Assert.Equal("Sftp", provider.StorageType);
        }

        [Fact]
        public async Task SaveBackupAsync_ConfigurationNotFound_ReturnsFailureWithoutConnecting()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            using var backupStream = new MemoryStream(new byte[] { 1, 2, 3 });
            var result = await provider.SaveBackupAsync(
                storageId: 999,
                backupStream: backupStream,
                fileName: "backup.bak",
                databaseName: "MiBaseDeDatos",
                databaseId: 1,
                databaseType: "SqlServer");

            Assert.False(result.success);
            Assert.Equal(string.Empty, result.filePath);
            Assert.Equal(0, result.fileSize);
            Assert.Contains("no encontrada", result.errorMessage, StringComparison.OrdinalIgnoreCase);

            // Debe haber quedado registrado el error en el histórico.
            var history = Assert.Single(context.BackupHistories);
            Assert.Equal("Error", history.Status);
            Assert.Equal("MiBaseDeDatos", history.DatabaseName);
        }

        [Fact]
        public async Task DeleteBackupAsync_ConfigurationNotFound_ReturnsFalseWithoutConnecting()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            var result = await provider.DeleteBackupAsync(storageId: 999, backupPath: "/backups/backup.zip", storageFileId: null);

            Assert.False(result);
        }

        [Fact]
        public async Task DownloadBackupAsync_ConfigurationNotFound_ReturnsFailureWithoutConnecting()
        {
            using var context = CreateContext();
            var provider = CreateProvider(context);

            var (success, stream, errorMessage) = await provider.DownloadBackupAsync(storageId: 999, backupPath: "/backups/backup.zip", storageFileId: null);

            Assert.False(success);
            Assert.Null(stream);
            Assert.Contains("no encontrada", errorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }
}
