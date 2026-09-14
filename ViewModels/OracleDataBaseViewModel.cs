using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de Oracle.
    /// </summary>
    public class OracleDataBaseViewModel
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
        public int Port { get; set; } = 1521;

        [Required(ErrorMessage = "El Service Name / SID es obligatorio")]
        [Display(Name = "Service Name / SID")]
        [MaxLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El usuario es obligatorio")]
        [Display(Name = "Usuario")]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Display(Name = "Contraseña")]
        [DataType(DataType.Password)]
        [MaxLength(500)]
        public string? Password { get; set; }

        [Display(Name = "Carpeta local de DATA_PUMP_DIR")]
        [MaxLength(500)]
        public string? DumpDirectoryPath { get; set; }
    }
}
