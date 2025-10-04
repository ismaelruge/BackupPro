using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa un registro de historial de backup.
    /// </summary>
    public class BackupHistory
    {
        /// <summary>
        /// Identificador único del historial de backup.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Id de la fuente de base de datos asociada.
        /// </summary>
        public int? DatabaseSourceId { get; set; }

        /// <summary>
        /// Nombre de la base de datos respaldada.
        /// </summary>
        [Required(ErrorMessage = "El nombre de la base de datos es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre de la base de datos no puede exceder 100 caracteres")]
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Fecha y hora del backup.
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Estado del backup (Exitoso/Fallido).
        /// </summary>
        [Required(ErrorMessage = "El estado es obligatorio")]
        [StringLength(20, ErrorMessage = "El estado no puede exceder 20 caracteres")]
        public string Status { get; set; } = string.Empty; // Exitoso / Fallido

        /// <summary>
        /// Mensaje adicional sobre el resultado del backup.
        /// </summary>
        [StringLength(500, ErrorMessage = "El mensaje no puede exceder 500 caracteres")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Ruta del archivo de backup generado.
        /// </summary>
        [StringLength(300, ErrorMessage = "La ruta del backup no puede exceder 300 caracteres")]
        public string BackupPath { get; set; } = string.Empty;
    }
}
