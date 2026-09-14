using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de SQLite.
    /// </summary>
    public class SqliteDataBaseViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ruta del archivo es obligatoria")]
        [Display(Name = "Ruta del Archivo")]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;
    }
}
