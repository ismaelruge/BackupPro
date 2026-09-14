using BackupPro.Services.Backup;
using Xunit;

namespace BackupPro.Tests.Services.Backup
{
    public class BackupProviderRegistryTests
    {
        private class FakeDatabaseProvider : IDatabaseBackupProvider
        {
            public string DatabaseType { get; }

            public FakeDatabaseProvider(string databaseType) => DatabaseType = databaseType;

            public Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId) =>
                throw new NotImplementedException();
        }

        private class FakeStorageProvider : IStorageProvider
        {
            public string StorageType { get; }

            public FakeStorageProvider(string storageType) => StorageType = storageType;

            public Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(
                int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId) =>
                throw new NotImplementedException();

            public Task<bool> DeleteBackupAsync(int storageId, string backupPath, string? storageFileId) =>
                throw new NotImplementedException();
        }

        private static BackupProviderRegistry CreateRegistry() => new(
            new IDatabaseBackupProvider[] { new FakeDatabaseProvider("SqlServer"), new FakeDatabaseProvider("MySQL") },
            new IStorageProvider[] { new FakeStorageProvider("Local"), new FakeStorageProvider("Ftp") });

        [Fact]
        public void ResolveDatabase_KnownType_ReturnsProvider()
        {
            var registry = CreateRegistry();

            var provider = registry.ResolveDatabase("SqlServer");

            Assert.NotNull(provider);
            Assert.Equal("SqlServer", provider!.DatabaseType);
        }

        [Fact]
        public void ResolveDatabase_IsCaseInsensitive()
        {
            var registry = CreateRegistry();

            var provider = registry.ResolveDatabase("sqlserver");

            Assert.NotNull(provider);
        }

        [Fact]
        public void ResolveDatabase_UnknownType_ReturnsNull()
        {
            var registry = CreateRegistry();

            var provider = registry.ResolveDatabase("Oracle");

            Assert.Null(provider);
        }

        [Fact]
        public void ResolveStorage_KnownType_ReturnsProvider()
        {
            var registry = CreateRegistry();

            var provider = registry.ResolveStorage("Ftp");

            Assert.NotNull(provider);
            Assert.Equal("Ftp", provider!.StorageType);
        }

        [Fact]
        public void ResolveStorage_UnknownType_ReturnsNull()
        {
            var registry = CreateRegistry();

            var provider = registry.ResolveStorage("S3");

            Assert.Null(provider);
        }

        [Fact]
        public void SupportedTypes_ExposeAllRegisteredProviders()
        {
            var registry = CreateRegistry();

            Assert.Equal(new[] { "SqlServer", "MySQL" }, registry.SupportedDatabaseTypes);
            Assert.Equal(new[] { "Local", "Ftp" }, registry.SupportedStorageTypes);
        }
    }
}
