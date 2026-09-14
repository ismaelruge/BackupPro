namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Genera el backup de una configuración de base de datos guardada.
    /// Cada motor soportado (SQL Server, MySQL, PostgreSQL, MongoDB, ...) tiene su propia
    /// implementación; agregar un motor nuevo consiste en escribir una clase que implemente esta
    /// interfaz y registrarla en el contenedor de dependencias — no requiere tocar el resto de la
    /// aplicación (ver <see cref="BackupProviderRegistry"/>).
    /// </summary>
    public interface IDatabaseBackupProvider
    {
        /// <summary>Identificador del tipo de base de datos (debe coincidir con TaskScheduler.DatabaseType).</summary>
        string DatabaseType { get; }

        /// <summary>
        /// Crea el backup y lo devuelve como <see cref="MemoryStream"/> en memoria. El llamador es
        /// responsable de liberar el stream devuelto.
        /// </summary>
        Task<(bool success, MemoryStream? backupStream, string fileName, string databaseName, string errorMessage)> CreateBackupAsync(int databaseId);

        /// <summary>
        /// Restaura <paramref name="backupZipStream"/> (el mismo .zip que devuelve
        /// <see cref="CreateBackupAsync"/>, con el dump original adentro) sobre la base de datos
        /// <paramref name="databaseId"/>. Operación destructiva: reemplaza los datos actuales de esa
        /// base de datos por los del backup. Usada por <see cref="BackupRestoreService"/>.
        /// </summary>
        Task<(bool success, string message)> RestoreBackupAsync(int databaseId, MemoryStream backupZipStream);
    }
}
