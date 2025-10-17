using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    public class MongoDBDataBase
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
        public int Port { get; set; } = 27017;

        [Required]
        [MaxLength(200)]
        public string DatabaseName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Username { get; set; }

        [MaxLength(500)]
        public string? Password { get; set; }

        [MaxLength(100)]
        public string? AuthenticationDatabase { get; set; } = "admin";

        public bool SslEnabled { get; set; } = false;

        [MaxLength(1000)]
        public string? ReplicaSet { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedAt { get; set; }
    }
}
