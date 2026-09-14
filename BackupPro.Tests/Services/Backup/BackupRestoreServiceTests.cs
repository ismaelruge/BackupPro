using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class BackupRestoreServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static BackupHistory CreateBackup(string status = "Exitoso", string? databaseType = "SqlServer", string? storageType = "Local", int? storageId = 1) => new()
        {
            DatabaseSourceId = 10,
            DatabaseName = "Ventas",
            Date = DateTime.Now.AddDays(-1),
            Status = status,
            Message = "ok",
            BackupPath = "/backups/ventas.zip",
            DatabaseType = databaseType,
            StorageType = storageType,
            StorageId = storageId
        };

        [Fact]
        public async Task RestoreAsync_BackupNotFound_ReturnsFailureWithoutLogging()
        {
            using var context = CreateContext();
            var registry = new BackupProviderRegistry(Array.Empty<IDatabaseBackupProvider>(), Array.Empty<IStorageProvider>());
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(999, "admin");

            Assert.False(success);
            Assert.Contains("no existe", message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(context.RestoreHistories);
        }

        [Fact]
        public async Task RestoreAsync_BackupWithErrorStatus_Refuses()
        {
            using var context = CreateContext();
            var backup = CreateBackup(status: "Error");
            context.BackupHistories.Add(backup);
            await context.SaveChangesAsync();

            var registry = new BackupProviderRegistry(Array.Empty<IDatabaseBackupProvider>(), Array.Empty<IStorageProvider>());
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(backup.Id, "admin");

            Assert.False(success);
            Assert.Contains("exitosos", message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RestoreAsync_LegacyBackupWithoutStorageOrDatabaseType_RefusesAndLogsHistory()
        {
            using var context = CreateContext();
            var backup = CreateBackup(databaseType: null, storageType: null, storageId: null);
            context.BackupHistories.Add(backup);
            await context.SaveChangesAsync();

            var registry = new BackupProviderRegistry(Array.Empty<IDatabaseBackupProvider>(), Array.Empty<IStorageProvider>());
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(backup.Id, "admin");

            Assert.False(success);
            Assert.Contains("antes de que existiera", message, StringComparison.OrdinalIgnoreCase);

            var logEntry = Assert.Single(context.RestoreHistories);
            Assert.Equal("Error", logEntry.Status);
            Assert.Equal("admin", logEntry.RestoredBy);
        }

        [Fact]
        public async Task RestoreAsync_DownloadFails_DoesNotAttemptRestoreAndLogsError()
        {
            using var context = CreateContext();
            var backup = CreateBackup();
            context.BackupHistories.Add(backup);
            await context.SaveChangesAsync();

            var databaseProvider = new Mock<IDatabaseBackupProvider>();
            databaseProvider.Setup(p => p.DatabaseType).Returns("SqlServer");

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");
            storageProvider
                .Setup(p => p.DownloadBackupAsync(1, backup.BackupPath, null))
                .ReturnsAsync((false, (MemoryStream?)null, "El archivo de backup ya no existe en disco."));

            var registry = new BackupProviderRegistry(new[] { databaseProvider.Object }, new[] { storageProvider.Object });
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(backup.Id, "admin");

            Assert.False(success);
            Assert.Contains("ya no existe", message);
            databaseProvider.Verify(p => p.RestoreBackupAsync(It.IsAny<int>(), It.IsAny<MemoryStream>()), Times.Never);

            var logEntry = Assert.Single(context.RestoreHistories);
            Assert.Equal("Error", logEntry.Status);
        }

        [Fact]
        public async Task RestoreAsync_DownloadAndRestoreSucceed_ReturnsSuccessAndLogsHistory()
        {
            using var context = CreateContext();
            var backup = CreateBackup();
            context.BackupHistories.Add(backup);
            await context.SaveChangesAsync();

            var databaseProvider = new Mock<IDatabaseBackupProvider>();
            databaseProvider.Setup(p => p.DatabaseType).Returns("SqlServer");
            databaseProvider
                .Setup(p => p.RestoreBackupAsync(10, It.IsAny<MemoryStream>()))
                .ReturnsAsync((true, "Base de datos 'Ventas' restaurada exitosamente desde el backup."));

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");
            storageProvider
                .Setup(p => p.DownloadBackupAsync(1, backup.BackupPath, null))
                .ReturnsAsync((true, new MemoryStream(new byte[] { 1, 2, 3 }), string.Empty));

            var registry = new BackupProviderRegistry(new[] { databaseProvider.Object }, new[] { storageProvider.Object });
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(backup.Id, "carlos.perez");

            Assert.True(success);
            Assert.Contains("restaurada exitosamente", message);

            var logEntry = Assert.Single(context.RestoreHistories);
            Assert.Equal("Exitoso", logEntry.Status);
            Assert.Equal("carlos.perez", logEntry.RestoredBy);
            Assert.Equal(backup.Id, logEntry.BackupHistoryId);
        }

        [Fact]
        public async Task RestoreAsync_UnsupportedCombination_ReturnsFailureWithoutThrowing()
        {
            using var context = CreateContext();
            var backup = CreateBackup(databaseType: "Oracle", storageType: "Local");
            context.BackupHistories.Add(backup);
            await context.SaveChangesAsync();

            var storageProvider = new Mock<IStorageProvider>();
            storageProvider.Setup(p => p.StorageType).Returns("Local");

            var registry = new BackupProviderRegistry(Array.Empty<IDatabaseBackupProvider>(), new[] { storageProvider.Object });
            var service = new BackupRestoreService(registry, context, NullLogger<BackupRestoreService>.Instance);

            var (success, message) = await service.RestoreAsync(backup.Id, "admin");

            Assert.False(success);
            Assert.Contains("no soportada", message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
