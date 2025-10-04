using System.ComponentModel.DataAnnotations;

namespace BackupPro.ViewModels
{
    /// <summary>
    /// Modelo de vista para el inicio de sesión de usuario.
    /// </summary>
    public class LoginViewModel
    {
        /// <summary>
        /// Usuario o correo electrónico del usuario.
        /// </summary>
        [Required(ErrorMessage = "El usuario o correo es obligatorio.")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Contraseña del usuario.
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
