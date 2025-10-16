using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de almacenamiento local.
    /// </summary>
    public class LocalStorageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ruta de la carpeta es obligatoria")]
        [Display(Name = "Ruta de la Carpeta")]
        [MaxLength(1000)]
        public string FolderPath { get; set; } = string.Empty;

        // Propiedades calculadas en tiempo de ejecución para la UI
        public bool IsAccessible { get; set; }
        public long? AvailableSpaceBytes { get; set; }
    }
}
