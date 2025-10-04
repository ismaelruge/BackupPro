using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa la configuración de la empresa y los datos de almacenamiento.
    /// </summary>
    public class CompanyConfig
    {
        /// <summary>
        /// Identificador único de la configuración.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Nombre de la empresa.
        /// </summary>
        [Required(ErrorMessage = "El nombre de la empresa es obligatorio")]
        [MaxLength(200, ErrorMessage = "El nombre de la empresa no puede exceder 200 caracteres")]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// Correo electrónico del administrador.
        /// </summary>
        [Required(ErrorMessage = "El correo del administrador es obligatorio")]
        [EmailAddress(ErrorMessage = "El correo electrónico no es válido")]
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>
        /// Lista de correos adicionales para notificaciones (separados por punto y coma).
        /// </summary>
        public string? AdditionalEmails { get; set; }
    }
}
