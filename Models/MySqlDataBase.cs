using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    public class MySqlDataBase
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string ConfigurationName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Host { get; set; } = string.Empty;

        [Required]
        public int Port { get; set; } = 3306;

        [Required]
        [MaxLength(200)]
        public string DatabaseName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Password { get; set; } = string.Empty;

        public bool SslMode { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedAt { get; set; }
    }
}
