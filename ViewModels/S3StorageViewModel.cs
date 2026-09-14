using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// ViewModel para crear/editar configuraciones de Amazon S3.
    /// </summary>
    public class S3StorageViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "El nombre de la configuración es obligatorio")]
        [Display(Name = "Nombre de Configuración")]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El Access Key Id es obligatorio")]
        [Display(Name = "Access Key Id")]
        [MaxLength(200)]
        public string AccessKeyId { get; set; } = string.Empty;

        [Required(ErrorMessage = "El Secret Access Key es obligatorio")]
        [Display(Name = "Secret Access Key")]
        [DataType(DataType.Password)]
        public string SecretAccessKey { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre del bucket es obligatorio")]
        [Display(Name = "Nombre del Bucket")]
        [MaxLength(255)]
        public string BucketName { get; set; } = string.Empty;

        [Required(ErrorMessage = "La región es obligatoria")]
        [Display(Name = "Región")]
        [MaxLength(50)]
        public string Region { get; set; } = string.Empty;

        [Display(Name = "Prefijo/Ruta en el Bucket")]
        [MaxLength(500)]
        public string? Prefix { get; set; }

        [Display(Name = "Configuración Activa")]
        public bool IsActive { get; set; } = true;

        // Propiedades de solo lectura para la UI
        public DateTime? LastConnectedAt { get; set; }
    }
}
