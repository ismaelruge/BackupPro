using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de Google Drive.
    /// </summary>
    public class GoogleDriveStorageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "El email no es válido")]
        [Display(Name = "Email de Google Drive")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "ID de Carpeta")]
        public string FolderId { get; set; } = string.Empty;

        [Display(Name = "Ruta de Carpeta")]
        public string FolderPath { get; set; } = string.Empty;

        [Display(Name = "Configuración Activa")]
        public bool IsActive { get; set; } = true;

        // Propiedades de solo lectura para la UI
        public DateTime? LastConnectedAt { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
    }
}
