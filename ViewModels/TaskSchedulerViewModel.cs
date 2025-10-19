using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear y editar tareas programadas
    /// </summary>
    public class TaskSchedulerViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la tarea es obligatorio")]
        [Display(Name = "Nombre de la Tarea")]
        [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string TaskName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El tipo de base de datos es obligatorio")]
        [Display(Name = "Tipo de Base de Datos")]
        [MaxLength(50)]
        public string DatabaseType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar una base de datos")]
        [Display(Name = "Base de Datos")]
        public int DatabaseId { get; set; }

        [Required(ErrorMessage = "El tipo de almacenamiento es obligatorio")]
        [Display(Name = "Tipo de Almacenamiento")]
        [MaxLength(50)]
        public string StorageType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe seleccionar un almacenamiento")]
        [Display(Name = "Almacenamiento")]
        public int StorageId { get; set; }

        [Required(ErrorMessage = "El tipo de frecuencia es obligatorio")]
        [Display(Name = "Tipo de Frecuencia")]
        [MaxLength(20)]
        public string FrequencyType { get; set; } = string.Empty;

        [Required(ErrorMessage = "El valor de frecuencia es obligatorio")]
        [Display(Name = "Frecuencia")]
        [Range(1, 999, ErrorMessage = "La frecuencia debe estar entre 1 y 999")]
        public int FrequencyValue { get; set; }

        [Display(Name = "Activo")]
        public bool IsActive { get; set; } = true;
    }
}
