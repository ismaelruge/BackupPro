using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa una configuración de almacenamiento en Azure Blob Storage.
    /// </summary>
    public class AzureBlobStorage
    {
        /// <summary>
        /// Identificador único de la configuración.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre descriptivo de la configuración.
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string ConfigurationName { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la cuenta de Azure Storage.
        /// </summary>
        [Required(ErrorMessage = "El nombre de la cuenta es obligatorio")]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Connection String de Azure Blob Storage (cifrado con AES-256-GCM antes de guardarse, ver <see cref="Services.CredentialProtector"/>).
        /// </summary>
        [Required(ErrorMessage = "La cadena de conexión es obligatoria")]
        [MaxLength(2000)]
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del contenedor donde se guardarán los backups.
        /// </summary>
        [Required(ErrorMessage = "El nombre del contenedor es obligatorio")]
        [MaxLength(200)]
        public string ContainerName { get; set; } = string.Empty;

        /// <summary>
        /// Ruta/prefijo dentro del contenedor (opcional).
        /// </summary>
        [MaxLength(1000)]
        public string BlobPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de creación de la configuración.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Fecha de última modificación.
        /// </summary>
        public DateTime? LastModifiedAt { get; set; }

        /// <summary>
        /// Usuario que creó la configuración.
        /// </summary>
        [MaxLength(256)]
        public string? CreatedBy { get; set; }
    }
}
