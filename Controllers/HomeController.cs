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
        private readonly EmailService _emailSender;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<HomeController> _logger;

        /// <summary>
        /// Inicializa una nueva instancia del <see cref="HomeController"/>.
        /// </summary>
        public HomeController(EmailService emailSender, UserManager<IdentityUser> userManager, ILogger<HomeController> logger)
        {
            _emailSender = emailSender;
            _userManager = userManager;
            _logger = logger;
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
        /// <returns>Redirige a la vista de login con un mensaje genérico.</returns>
        /// <remarks>
        /// Es POST (no GET) y requiere token antifalsificación porque cambia el estado de la cuenta;
        /// además siempre responde con el mismo mensaje exista o no la cuenta, para no permitir que
        /// alguien use este formulario para averiguar qué correos están registrados en el sistema.
        /// </remarks>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            const string genericMessage = "Si existe una cuenta con ese correo, se envió una nueva contraseña.";

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Debes ingresar tu correo electrónico.";
                return RedirectToAction("Login", "Account");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // Generar una nueva contraseña aleatoria y resetearla
                var newPassword = GenerateRandomPassword();
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    bool sent = await _emailSender.SendEmailAsync(email, "Nueva contraseña",
                        $"Tu nueva contraseña es: <strong>{newPassword}</strong>");

                    if (!sent)
                    {
                        _logger.LogError("La contraseña de {Email} fue reseteada pero el correo de notificación no pudo enviarse.", email);
                    }
                }
                else
                {
                    _logger.LogError("Error al resetear la contraseña de {Email}: {Errors}", email,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }

            // Mismo mensaje exista o no la cuenta, y exista o no falla en enviar el correo: no se
            // revela información sobre qué cuentas existen ni sobre errores internos de envío.
            TempData["Success"] = genericMessage;
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
            return RedirectToAction("Index");
        }
    }
}
