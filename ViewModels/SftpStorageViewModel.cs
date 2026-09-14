using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de SFTP.
    /// </summary>
    public class SftpStorageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El servidor SFTP es obligatorio")]
        [Display(Name = "Servidor SFTP")]
        [MaxLength(200)]
        public string Host { get; set; } = string.Empty;

        [Required(ErrorMessage = "El puerto es obligatorio")]
        [Display(Name = "Puerto")]
        [Range(1, 65535, ErrorMessage = "El puerto debe estar entre 1 y 65535")]
        public int Port { get; set; } = 22;

        [Required(ErrorMessage = "El usuario es obligatorio")]
        [Display(Name = "Usuario")]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [Display(Name = "Contraseña")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ruta remota es obligatoria")]
        [Display(Name = "Ruta en el Servidor")]
        public string RemotePath { get; set; } = "/";

        [Display(Name = "Configuración Activa")]
        public bool IsActive { get; set; } = true;

        // Propiedades de solo lectura para la UI
        public DateTime? LastConnectedAt { get; set; }
    }
}
