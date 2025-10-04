using System.ComponentModel.DataAnnotations;

namespace BackupPro.Models
{
    /// <summary>
    /// Representa un origen de base de datos a respaldar.
    /// </summary>
    public class DatabaseSource
    {
        /// <summary>
        /// Identificador único del origen.
        /// </summary>
        [Key]
        public int? Id { get; set; }

        /// <summary>
        /// Nombre descriptivo para el sistema.
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [MaxLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Tipo de base de datos (SQLServer, PostgreSQL, MySQL, MongoDB).
        /// </summary>
        [Required(ErrorMessage = "El tipo de base de datos es obligatorio")]
        [MaxLength(50, ErrorMessage = "El tipo no puede exceder 50 caracteres")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Servidor o instancia (localhost, IP, etc.).
        /// </summary>
        [Required(ErrorMessage = "El servidor es obligatorio")]
        [MaxLength(200, ErrorMessage = "El servidor no puede exceder 200 caracteres")]
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Base de datos seleccionada en el servidor.
        /// </summary>
        [MaxLength(200, ErrorMessage = "El nombre de la base de datos no puede exceder 200 caracteres")]
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Usuario de la conexión.
        /// </summary>
        [MaxLength(200, ErrorMessage = "El usuario no puede exceder 200 caracteres")]
        public string User { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña de conexión.
        /// </summary>
        [MaxLength(200, ErrorMessage = "La contraseña no puede exceder 200 caracteres")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Indica si se usa autenticación de Windows (solo SQL Server).
        /// </summary>
        public bool UseWindowsAuth { get; set; } = false;

        /// <summary>
        /// Nombre del archivo de respaldo (.bak, .sql, etc.).
        /// </summary>
        [MaxLength(200, ErrorMessage = "El nombre del backup no puede exceder 200 caracteres")]
        public string BackupName { get; set; } = string.Empty;
    }
}
