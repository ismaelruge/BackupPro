using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// Modelo para confirmar la restauración de un backup: exige escribir el nombre exacto de la
    /// base de datos (como una segunda confirmación explícita, además del botón) y la contraseña
    /// actual del usuario, ya que restaurar reemplaza los datos existentes sin posibilidad de
    /// deshacerlo.
    /// </summary>
    public class RestoreConfirmViewModel
    {
        public int BackupHistoryId { get; set; }

        public string DatabaseName { get; set; } = string.Empty;

        public string DatabaseType { get; set; } = string.Empty;

        public string StorageType { get; set; } = string.Empty;

        public DateTime BackupDate { get; set; }

        [Required(ErrorMessage = "Debes escribir el nombre de la base de datos para confirmar")]
        [Display(Name = "Nombre de la base de datos")]
        public string ConfirmDatabaseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debes ingresar tu contraseña actual para confirmar")]
        [Display(Name = "Tu contraseña actual")]
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
