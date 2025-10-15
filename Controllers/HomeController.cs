using BackupPro.Models;
using BackupPro.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador principal para la página de inicio y utilidades generales.
    /// </summary>
    public class HomeController : Controller
    {
        //private readonly BackupService _backupService;
        private readonly EmailService _emailSender;
        private readonly UserManager<IdentityUser> _userManager;

        /// <summary>
        /// Inicializa una nueva instancia del <see cref="HomeController"/>.
        /// </summary>
        public HomeController(EmailService emailSender, UserManager<IdentityUser> userManager)
        {
            _emailSender = emailSender;
            _userManager = userManager;
        }

        /// <summary>
        /// Página principal del sistema.
        /// </summary>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Muestra la vista de error con el identificador de la solicitud.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Permite recuperar la contraseña de un usuario enviando una nueva por correo.
        /// </summary>
        /// <param name="email">Correo electrónico del usuario.</param>
        /// <returns>Redirige a la vista de login con mensaje de éxito o error.</returns>
        [HttpGet]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Debes ingresar tu correo electrónico.";
                return RedirectToAction("Login", "Account");
            }

            // Buscar todos los usuarios con ese correo
            var users = _userManager.Users.Where(u => u.Email == email).ToList();

            if (users.Count == 0)
            {
                TempData["Error"] = "No existe una cuenta con este correo.";
                return RedirectToAction("Login", "Account");
            }

            if (users.Count > 1)
            {
                // Eliminar todos menos el primero
                var userToKeep = users.First();
                foreach (var duplicate in users.Skip(1))
                {
                    await _userManager.DeleteAsync(duplicate);
                }

                // Actualizamos la lista para que solo quede uno
                users = new List<IdentityUser> { userToKeep };
            }

            var user = users.First();

            // Generar una nueva contraseña aleatoria
            var newPassword = GenerateRandomPassword();

            // Resetear la contraseña
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                // Enviar correo con la nueva contraseña
                await _emailSender.SendEmailAsync(email, "Nueva contraseña",
                    $"Tu nueva contraseña es: <strong>{newPassword}</strong>");

                TempData["Success"] = "Se generó una nueva contraseña y se envió a tu correo.";
            }
            else
            {
                TempData["Error"] = "Error al generar la nueva contraseña: " +
                                    string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction("Login", "Account");
        }

        /// <summary>
        /// Genera una contraseña aleatoria segura.
        /// </summary>
        /// <param name="length">Longitud de la contraseña (por defecto 10).</param>
        /// <returns>Contraseña generada.</returns>
        private string GenerateRandomPassword(int length = 10)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789*#@!";
            var data = new byte[length];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(data);

            var sb = new StringBuilder(length);
            foreach (var b in data)
            {
                sb.Append(chars[b % chars.Length]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ejecuta los backups manualmente desde la interfaz.
        /// </summary>
        public async Task<IActionResult> EjecutarBackups()
        {
            //await _backupService.EjecutarBackupsAsync();
            return RedirectToAction("Index");
        }
    }
}
