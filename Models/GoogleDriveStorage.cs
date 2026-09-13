using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa una configuración de almacenamiento en Google Drive.
    /// </summary>
    public class GoogleDriveStorage
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
        /// Email de la cuenta de Google Drive.
        /// </summary>
        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// ID de la carpeta en Google Drive donde se guardarán los backups.
        /// </summary>
        [MaxLength(500)]
        public string FolderId { get; set; } = string.Empty;

        /// <summary>
        /// Ruta legible de la carpeta (para mostrar en UI).
        /// </summary>
        [MaxLength(1000)]
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// Access Token (cifrado con AES-256-GCM antes de guardarse, ver <see cref="Services.CredentialProtector"/>).
        /// </summary>
        [MaxLength(4000)]
        public string? AccessToken { get; set; }

        /// <summary>
        /// Refresh Token (cifrado con AES-256-GCM antes de guardarse, ver <see cref="Services.CredentialProtector"/>).
        /// </summary>
        [MaxLength(4000)]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Fecha de expiración del Access Token.
        /// </summary>
        public DateTime? TokenExpiresAt { get; set; }

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
