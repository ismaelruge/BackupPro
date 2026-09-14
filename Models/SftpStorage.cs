using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa una configuración de almacenamiento en servidor SFTP (SSH File Transfer Protocol).
    /// </summary>
    public class SftpStorage
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
        /// Servidor SFTP (host).
        /// </summary>
        [Required(ErrorMessage = "El servidor SFTP es obligatorio")]
        [MaxLength(200)]
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Puerto del servidor SFTP (por defecto 22).
        /// </summary>
        [Range(1, 65535, ErrorMessage = "El puerto debe estar entre 1 y 65535")]
        public int Port { get; set; } = 22;

        /// <summary>
        /// Usuario para autenticación SFTP.
        /// </summary>
        [Required(ErrorMessage = "El usuario es obligatorio")]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña para autenticación SFTP (cifrada con AES-256-GCM antes de guardarse, ver <see cref="Services.CredentialProtector"/>).
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MaxLength(500)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Ruta en el servidor SFTP donde se guardarán los backups.
        /// </summary>
        [Required(ErrorMessage = "La ruta es obligatoria")]
        [MaxLength(1000)]
        public string RemotePath { get; set; } = "/";

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
