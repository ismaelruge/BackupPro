using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa el histórico de backups realizados
    /// </summary>
    public class BackupHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DatabaseSourceId { get; set; }

        [Required]
        [MaxLength(200)]
        public string DatabaseName { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string BackupPath { get; set; } = string.Empty;
    }
}
