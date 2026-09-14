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

        /// <summary>
        /// Sube <paramref name="backupStream"/> al destino de almacenamiento indicado.
        /// <paramref name="databaseType"/> (p.ej. "SqlServer") se guarda en el histórico junto con el
        /// resto de metadatos del backup, para que <see cref="BackupRestoreService"/> sepa después
        /// con qué motor restaurarlo.
        /// </summary>
        Task<(bool success, string filePath, long fileSize, string errorMessage)> SaveBackupAsync(int storageId, MemoryStream backupStream, string fileName, string databaseName, int databaseId, string databaseType);

        /// <summary>
        /// Borra un backup ya subido, identificado por <paramref name="backupPath"/> (y, en destinos
        /// donde la ruta no alcanza para borrar como Google Drive/OneDrive, por
        /// <paramref name="storageFileId"/>). Usado por <see cref="BackupRetentionService"/> al
        /// aplicar la política de retención. Devuelve true solo si el archivo quedó efectivamente
        /// borrado (o ya no existía); false si no se pudo borrar, para que el llamador conserve el
        /// registro en el histórico y reintente más adelante.
        /// </summary>
        Task<bool> DeleteBackupAsync(int storageId, string backupPath, string? storageFileId);

        /// <summary>
        /// Descarga un backup ya subido, identificado igual que en <see cref="DeleteBackupAsync"/>.
        /// Usado por <see cref="BackupRestoreService"/> para traer de vuelta el .zip antes de
        /// restaurarlo. El llamador es responsable de liberar el stream devuelto.
        /// </summary>
        Task<(bool success, MemoryStream? stream, string errorMessage)> DownloadBackupAsync(int storageId, string backupPath, string? storageFileId);
    }
}
