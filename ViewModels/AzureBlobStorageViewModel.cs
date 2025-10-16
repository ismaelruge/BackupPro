using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de Azure Blob Storage.
    /// </summary>
    public class AzureBlobStorageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre de la cuenta es obligatorio")]
        [Display(Name = "Nombre de la Cuenta")]
        [MaxLength(200)]
        public string AccountName { get; set; } = string.Empty;

        [Required(ErrorMessage = "La cadena de conexión es obligatoria")]
        [Display(Name = "Cadena de Conexión")]
        [DataType(DataType.Password)]
        public string ConnectionString { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del contenedor es obligatorio")]
        [Display(Name = "Nombre del Contenedor")]
        [MaxLength(200)]
        public string ContainerName { get; set; } = string.Empty;

        [Display(Name = "Prefijo/Ruta en el Contenedor")]
        public string BlobPrefix { get; set; } = string.Empty;

        [Display(Name = "Configuración Activa")]
        public bool IsActive { get; set; } = true;

        // Propiedades de solo lectura para la UI
        public DateTime? LastConnectedAt { get; set; }
    }
}
