using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa el histórico de backups realizados
    /// </summary>
    public class BackupHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DatabaseSourceId { get; set; }

        [Required]
        [MaxLength(200)]
        public string DatabaseName { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string BackupPath { get; set; } = string.Empty;

        /// <summary>
        /// Tipo y de almacenamiento donde quedó guardado el backup (p.ej. "Local", "Ftp"), y su Id
        /// de configuración. Nulos en registros de error (nunca hubo archivo) y en backups creados
        /// antes de que existiera esta columna. Usado por <see cref="Services.Backup.BackupRetentionService"/>
        /// para saber con qué <see cref="Services.Backup.IStorageProvider"/> y configuración borrar
        /// el archivo real al aplicar la política de retención.
        /// </summary>
        [MaxLength(50)]
        public string? StorageType { get; set; }

        public int? StorageId { get; set; }

        /// <summary>
        /// Identificador nativo del archivo en el destino de almacenamiento (p.ej. el file id de
        /// Google Drive o el item id de OneDrive), cuando <see cref="BackupPath"/> por sí solo no
        /// alcanza para borrarlo. Null en destinos donde la ruta ya es suficiente (local, FTP, Blob).
        /// </summary>
        [MaxLength(500)]
        public string? StorageFileId { get; set; }

        /// <summary>
        /// Tipo de motor de base de datos del backup (p.ej. "SqlServer", "MySQL"). Nulo en registros
        /// de error y en backups creados antes de que existiera esta columna. Usado por
        /// <see cref="Services.Backup.BackupRestoreService"/> para saber con qué
        /// <see cref="Services.Backup.IDatabaseBackupProvider"/> restaurar el backup.
        /// </summary>
        [MaxLength(50)]
        public string? DatabaseType { get; set; }
    }
}
