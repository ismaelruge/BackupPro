using BackupPro.Data;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class BackupExecutionServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static Models.TaskScheduler CreateTask(string databaseType = "SqlServer", string storageType = "Local") => new()
        {
            Id = 1,
            TaskName = "Tarea de prueba",
            DatabaseType = databaseType,
            DatabaseId = 10,
            StorageType = storageType,
            StorageId = 20,
            FrequencyType = "hours",
            FrequencyValue = 1
        };

        [Fact]
        public async Task ExecuteAsync_UnsupportedCombination_ReturnsMessageWithoutThrowing()
        {
            var registry = new BackupProviderRegistry(
                Array.Empty<IDatabaseBackupProvider>(),
                Array.Empty<IStorageProvider>());

            using var context = CreateContext();
            var service = new BackupExecutionService(registry, context, NullLogger<BackupExecutionService>.Instance);

            var result = await service.ExecuteAsync(CreateTask());

            Assert.Contains("no soportada", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExecuteAsync_DatabaseBackupFails_ThrowsAndLogsErrorToHistory()
        {
            var databaseProvider = new Mock<IDatabaseBackupProvider>();
            databaseProvider.Setup(p => p.DatabaseType).Returns("SqlServer");
            databaseProvider
                .Setup(p => p.CreateBackupAsync(It.IsAny<int>()))
                .ReturnsAsync((false, (MemoryStream?)null, "backup.bak", "MiBaseDeDatos", "No se encontró sqlcmd"));

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");

            var registry = new BackupProviderRegistry(
                new[] { databaseProvider.Object },
                new[] { storageProvider.Object });

            using var context = CreateContext();
            var service = new BackupExecutionService(registry, context, NullLogger<BackupExecutionService>.Instance);

            await Assert.ThrowsAsync<Exception>(() => service.ExecuteAsync(CreateTask()));

            var history = Assert.Single(context.BackupHistories);
            Assert.Equal("Error", history.Status);
            Assert.Equal("MiBaseDeDatos", history.DatabaseName);
            Assert.Equal("No se encontró sqlcmd", history.Message);

            storageProvider.Verify(p => p.SaveBackupAsync(It.IsAny<int>(), It.IsAny<MemoryStream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_StorageSaveFails_ThrowsWithoutDoubleLoggingHistory()
        {
            var databaseProvider = new Mock<IDatabaseBackupProvider>();
            databaseProvider.Setup(p => p.DatabaseType).Returns("SqlServer");
            databaseProvider
                .Setup(p => p.CreateBackupAsync(It.IsAny<int>()))
                .ReturnsAsync((true, new MemoryStream(new byte[] { 1, 2, 3 }), "backup.bak", "MiBaseDeDatos", string.Empty));

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");
            storageProvider
                .Setup(p => p.SaveBackupAsync(It.IsAny<int>(), It.IsAny<MemoryStream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((false, string.Empty, 0L, "Disco lleno"));

            var registry = new BackupProviderRegistry(
                new[] { databaseProvider.Object },
                new[] { storageProvider.Object });

            using var context = CreateContext();
            var service = new BackupExecutionService(registry, context, NullLogger<BackupExecutionService>.Instance);

            var ex = await Assert.ThrowsAsync<Exception>(() => service.ExecuteAsync(CreateTask()));

            Assert.Contains("Disco lleno", ex.Message);
            // El registro del error en el histórico es responsabilidad del IStorageProvider (mockeado
            // acá), no de BackupExecutionService: no debe haber quedado un segundo registro.
            Assert.Empty(context.BackupHistories);
        }

        [Fact]
        public async Task ExecuteAsync_Success_ReturnsMessageWithFileNameAndFormattedSize()
        {
            var databaseProvider = new Mock<IDatabaseBackupProvider>();
            databaseProvider.Setup(p => p.DatabaseType).Returns("SqlServer");
            databaseProvider
                .Setup(p => p.CreateBackupAsync(It.IsAny<int>()))
                .ReturnsAsync((true, new MemoryStream(new byte[] { 1, 2, 3 }), "backup.bak", "MiBaseDeDatos", string.Empty));

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");
            storageProvider
                .Setup(p => p.SaveBackupAsync(It.IsAny<int>(), It.IsAny<MemoryStream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((true, "/backups/backup.bak", 2048L, string.Empty));

            var registry = new BackupProviderRegistry(
                new[] { databaseProvider.Object },
                new[] { storageProvider.Object });

            using var context = CreateContext();
            var service = new BackupExecutionService(registry, context, NullLogger<BackupExecutionService>.Instance);

            var result = await service.ExecuteAsync(CreateTask());

            Assert.Contains("backup.bak", result);
            Assert.Contains("2 KB", result);
            Assert.Empty(context.BackupHistories);
        }
    }
}
