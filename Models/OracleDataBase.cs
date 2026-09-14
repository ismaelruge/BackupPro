using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    public class OracleDataBase
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
        public int Port { get; set; } = 1521;

        /// <summary>
        /// Service Name (o SID) de la base de datos Oracle. A diferencia de los demás motores
        /// soportados, Oracle identifica la base a la que conectarse mediante un Service Name/SID en
        /// vez de un nombre de base de datos simple.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string ServiceName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Ruta local (en el sistema de archivos de ESTA app) que corresponde al objeto DIRECTORY
        /// <c>DATA_PUMP_DIR</c> del servidor Oracle. Data Pump (expdp/impdp) siempre escribe/lee del
        /// lado del servidor Oracle, no de la máquina que ejecuta BackupPro; este campo solo tiene
        /// sentido cuando <see cref="Host"/> es el mismo host que corre BackupPro (localhost/127.0.0.1),
        /// de modo que la carpeta del DIRECTORY sea visible directamente en el disco local. Si se deja
        /// vacío, o el host no es local, el provider de backup no podrá recuperar/enviar el archivo
        /// .dmp y lo informará como una limitación conocida en vez de fingir éxito.
        /// </summary>
        [MaxLength(500)]
        public string? DumpDirectoryPath { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(200)]
        public string? CreatedBy { get; set; }

        public DateTime? LastModifiedAt { get; set; }
    }
}
