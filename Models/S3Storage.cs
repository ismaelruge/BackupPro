using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa una configuración de almacenamiento en Amazon S3.
    /// </summary>
    public class S3Storage
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
        /// Access Key Id de la credencial de AWS.
        /// </summary>
        [Required(ErrorMessage = "El Access Key Id es obligatorio")]
        [MaxLength(200)]
        public string AccessKeyId { get; set; } = string.Empty;

        /// <summary>
        /// Secret Access Key de la credencial de AWS (cifrado con AES-256-GCM antes de guardarse, ver <see cref="Services.CredentialProtector"/>).
        /// </summary>
        [Required(ErrorMessage = "El Secret Access Key es obligatorio")]
        [MaxLength(500)]
        public string SecretAccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Nombre del bucket donde se guardarán los backups.
        /// </summary>
        [Required(ErrorMessage = "El nombre del bucket es obligatorio")]
        [MaxLength(255)]
        public string BucketName { get; set; } = string.Empty;

        /// <summary>
        /// Región de AWS donde vive el bucket (p.ej. "us-east-1").
        /// </summary>
        [Required(ErrorMessage = "La región es obligatoria")]
        [MaxLength(50)]
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Prefijo/"carpeta" dentro del bucket (opcional).
        /// </summary>
        [MaxLength(500)]
        public string? Prefix { get; set; }

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
