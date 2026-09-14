namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Resuelve el <see cref="IDatabaseBackupProvider"/> o <see cref="IStorageProvider"/> que
    /// corresponde a un tipo de base de datos / almacenamiento. Todos los providers registrados en
    /// el contenedor de dependencias se indexan una sola vez por instancia (el registro es Scoped).
    /// </summary>
    public class BackupProviderRegistry
    {
        private readonly Dictionary<string, IDatabaseBackupProvider> _databaseProviders;
        private readonly Dictionary<string, IStorageProvider> _storageProviders;

        public BackupProviderRegistry(IEnumerable<IDatabaseBackupProvider> databaseProviders, IEnumerable<IStorageProvider> storageProviders)
        {
            _databaseProviders = databaseProviders.ToDictionary(p => p.DatabaseType, StringComparer.OrdinalIgnoreCase);
            _storageProviders = storageProviders.ToDictionary(p => p.StorageType, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyCollection<string> SupportedDatabaseTypes => _databaseProviders.Keys;

        public IReadOnlyCollection<string> SupportedStorageTypes => _storageProviders.Keys;

        public IDatabaseBackupProvider? ResolveDatabase(string databaseType) =>
            _databaseProviders.GetValueOrDefault(databaseType);

        public IStorageProvider? ResolveStorage(string storageType) =>
            _storageProviders.GetValueOrDefault(storageType);
    }
}
