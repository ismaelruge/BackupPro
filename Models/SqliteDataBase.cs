using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    public class SqliteDataBase
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string FilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedAt { get; set; }
    }
}
