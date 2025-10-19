using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa una tarea programada para ejecutar backups automáticos
    /// </summary>
    public class TaskScheduler
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string DatabaseType { get; set; } = string.Empty;

        [Required]
        public int DatabaseId { get; set; }

        [Required]
        [MaxLength(50)]
        public string StorageType { get; set; } = string.Empty;

        [Required]
        public int StorageId { get; set; }

        [Required]
        [MaxLength(20)]
        public string FrequencyType { get; set; } = string.Empty;

        [Required]
        public int FrequencyValue { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastRunAt { get; set; }

        public DateTime? NextRunAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedAt { get; set; }
    }
}
