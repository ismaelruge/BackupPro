using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de SQL Server.
    /// </summary>
    public class SqlServerDataBaseViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El host/servidor es obligatorio")]
        [Display(Name = "Host/Servidor")]
        [MaxLength(255)]
        public string Host { get; set; } = string.Empty;

        [Required(ErrorMessage = "El puerto es obligatorio")]
        [Display(Name = "Puerto")]
        [Range(1, 65535, ErrorMessage = "El puerto debe estar entre 1 y 65535")]
        public int Port { get; set; } = 1433;

        [Required(ErrorMessage = "El nombre de la base de datos es obligatorio")]
        [Display(Name = "Nombre de Base de Datos")]
        [MaxLength(200)]
        public string DatabaseName { get; set; } = string.Empty;

        [Display(Name = "Usuario")]
        [MaxLength(200)]
        public string? Username { get; set; }

        [Display(Name = "Contraseña")]
        [DataType(DataType.Password)]
        [MaxLength(500)]
        public string? Password { get; set; }

        [Display(Name = "Seguridad Integrada (Windows Authentication)")]
        public bool IntegratedSecurity { get; set; } = false;

        [Display(Name = "Confiar en Certificado del Servidor")]
        public bool TrustServerCertificate { get; set; } = false;
    }
}
