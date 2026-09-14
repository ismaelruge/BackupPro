using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// Modelo de vista para la configuración básica de la empresa.
    /// </summary>
    public class CompanyConfigViewModel
    {
        /// <summary>ID de la configuración.</summary>
        public int Id { get; set; }

        /// <summary>Nombre de la empresa.</summary>
        [Required(ErrorMessage = "El nombre de la empresa es obligatorio")]
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>Email del administrador (usuario logueado).</summary>
        public string AdminEmail { get; set; } = string.Empty;

        /// <summary>Nombre de usuario del administrador (usuario logueado).</summary>
        public string AdminUserName { get; set; } = string.Empty;

        /// <summary>Contraseña del administrador (opcional, solo para actualización).</summary>
        [MinLength(12, ErrorMessage = "La contraseña debe tener al menos 12 caracteres, con mayúsculas, minúsculas, números y símbolos")]
        public string? AdminPassword { get; set; }

        /// <summary>Contraseña actual del administrador, requerida para poder cambiar <see cref="AdminPassword"/>.</summary>
        public string? CurrentPassword { get; set; }

        /// <summary>Correos adicionales para notificaciones (separados por punto y coma en BD).</summary>
        public string? AdditionalEmails { get; set; }

        /// <summary>Lista de correos adicionales para la vista.</summary>
        public List<string> EmailList { get; set; } = new List<string>();
    }
}
