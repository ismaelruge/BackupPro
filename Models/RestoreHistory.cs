using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Registro de auditoría de cada intento de restauración (exitoso o no): qué backup se restauró,
    /// cuándo y quién lo hizo. A diferencia de <see cref="BackupHistory"/>, estas filas nunca se
    /// borran automáticamente (ni por la política de retención ni por "Limpiar historial" de
    /// backups): restaurar es una operación destructiva y su rastro debe quedar.
    /// </summary>
    public class RestoreHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BackupHistoryId { get; set; }

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

        /// <summary>Usuario que confirmó la restauración (su contraseña se verificó antes de ejecutarla).</summary>
        [MaxLength(200)]
        public string? RestoredBy { get; set; }
    }
}
