using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class BackupRetentionServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static BackupHistory Backup(int databaseSourceId, string databaseName, DateTime date, string storageType = "Local", int storageId = 1, string status = "Exitoso") => new()
        {
            DatabaseSourceId = databaseSourceId,
            DatabaseName = databaseName,
            Date = date,
            Status = status,
            Message = "ok",
            BackupPath = $"/backups/{databaseName}-{date:yyyyMMdd}.zip",
            StorageType = storageType,
            StorageId = storageId
        };

        private static BackupRetentionService CreateService(ApplicationDbContext context, Mock<IStorageProvider> storageProviderMock)
        {
            storageProviderMock.Setup(p => p.StorageType).Returns("Local");
            var registry = new BackupProviderRegistry(
                Array.Empty<IDatabaseBackupProvider>(),
                new[] { storageProviderMock.Object });

            return new BackupRetentionService(context, registry, NullLogger<BackupRetentionService>.Instance);
        }

        [Fact]
        public async Task RunAsync_NoBackupsOlderThanSixMonths_DeletesNothing()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            context.BackupHistories.AddRange(
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20)),
                Backup(1, "Ventas", now.AddDays(-30)));
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(3, context.BackupHistories.Count());
            storageProviderMock.Verify(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_OldBackupsButFewerThanThreeRecent_DeletesNothing()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            context.BackupHistories.AddRange(
                Backup(1, "Ventas", now.AddMonths(-8)),
                Backup(1, "Ventas", now.AddMonths(-7)),
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20))); // solo 2 recientes (< 6 meses)
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(4, context.BackupHistories.Count());
            storageProviderMock.Verify(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_OldBackupsWithThreeOrMoreRecent_DeletesOnlyOldOnes()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            var old1 = Backup(1, "Ventas", now.AddMonths(-8));
            var old2 = Backup(1, "Ventas", now.AddMonths(-7));
            context.BackupHistories.AddRange(
                old1,
                old2,
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20)),
                Backup(1, "Ventas", now.AddDays(-30))); // 3 recientes (cumple el mínimo)
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            storageProviderMock
                .Setup(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(3, context.BackupHistories.Count());
            Assert.DoesNotContain(context.BackupHistories, h => h.Id == old1.Id);
            Assert.DoesNotContain(context.BackupHistories, h => h.Id == old2.Id);
            storageProviderMock.Verify(p => p.DeleteBackupAsync(1, It.IsAny<string>(), null), Times.Exactly(2));
        }

        [Fact]
        public async Task RunAsync_ProviderFailsToDeleteFile_KeepsHistoryRow()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            var old = Backup(1, "Ventas", now.AddMonths(-8));
            context.BackupHistories.AddRange(
                old,
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20)),
                Backup(1, "Ventas", now.AddDays(-30)));
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            storageProviderMock
                .Setup(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(false); // p.ej. no se pudo conectar al FTP

            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(4, context.BackupHistories.Count());
            Assert.Contains(context.BackupHistories, h => h.Id == old.Id);
        }

        [Fact]
        public async Task RunAsync_BackupsWithoutStorageInfo_AreIgnored()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            // Simula backups creados antes de que existiera la columna StorageType/StorageId.
            var legacyOld = new BackupHistory
            {
                DatabaseSourceId = 1,
                DatabaseName = "Ventas",
                Date = now.AddMonths(-8),
                Status = "Exitoso",
                Message = "ok",
                BackupPath = "/backups/legacy.zip",
                StorageType = null,
                StorageId = null
            };
            context.BackupHistories.AddRange(
                legacyOld,
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20)),
                Backup(1, "Ventas", now.AddDays(-30)));
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            storageProviderMock
                .Setup(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(4, context.BackupHistories.Count());
            Assert.Contains(context.BackupHistories, h => h.Id == legacyOld.Id);
        }

        [Fact]
        public async Task RunAsync_ErrorStatusBackups_AreNeverConsideredForDeletion()
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            context.BackupHistories.AddRange(
                Backup(1, "Ventas", now.AddMonths(-8), status: "Error"),
                Backup(1, "Ventas", now.AddDays(-10)),
                Backup(1, "Ventas", now.AddDays(-20)),
                Backup(1, "Ventas", now.AddDays(-30)));
            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.Equal(4, context.BackupHistories.Count());
            storageProviderMock.Verify(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task RunAsync_DifferentSeries_EvaluatedIndependently()
        {
            using var context = CreateContext();
            var now = DateTime.Now;

            // Serie A: 3 recientes -> se borran los viejos.
            var oldA = Backup(1, "Ventas", now.AddMonths(-8), storageId: 1);
            context.BackupHistories.AddRange(
                oldA,
                Backup(1, "Ventas", now.AddDays(-10), storageId: 1),
                Backup(1, "Ventas", now.AddDays(-20), storageId: 1),
                Backup(1, "Ventas", now.AddDays(-30), storageId: 1));

            // Serie B (mismo nombre, otro storageId): solo 1 reciente -> no se borra nada.
            var oldB = Backup(1, "Ventas", now.AddMonths(-8), storageId: 2);
            context.BackupHistories.AddRange(
                oldB,
                Backup(1, "Ventas", now.AddDays(-10), storageId: 2));

            await context.SaveChangesAsync();

            var storageProviderMock = new Mock<IStorageProvider>();
            storageProviderMock
                .Setup(p => p.DeleteBackupAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>()))
                .ReturnsAsync(true);
            var service = CreateService(context, storageProviderMock);

            await service.RunAsync();

            Assert.DoesNotContain(context.BackupHistories, h => h.Id == oldA.Id);
            Assert.Contains(context.BackupHistories, h => h.Id == oldB.Id);
        }
    }
}
