namespace BackupPro.Services.Backup
{
    /// <summary>
    /// Sube el backup ya generado a un destino de almacenamiento guardado (disco local, FTP, Azure
    /// Blob, Google Drive, OneDrive, ...). Agregar un destino nuevo consiste en escribir una clase
    /// que implemente esta interfaz y registrarla en el contenedor de dependencias — no requiere
    /// tocar el resto de la aplicación (ver <see cref="BackupProviderRegistry"/>).
    /// </summary>
    public interface IStorageProvider
    {
        /// <summary>Identificador del tipo de almacenamiento (debe coincidir con TaskScheduler.StorageType).</summary>
        string StorageType { get; }

        /// <summary>Sube <paramref name="backupStream"/> al destino de almacenamiento indicado.</summary>
        Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId);
    }
}
