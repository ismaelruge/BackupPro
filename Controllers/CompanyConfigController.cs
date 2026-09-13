using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador para la configuración básica de la empresa.
    /// </summary>
    [Authorize]
    public class CompanyConfigController : Controller
    {
        #region Campos
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly EmailService _emailService;
        private readonly ILogger<CompanyConfigController> _logger;
        #endregion

        #region Constructor
        /// <summary>
        /// Inicializa una nueva instancia del <see cref="CompanyConfigController"/>.
        /// </summary>
        /// <param name="context">Contexto de base de datos.</param>
        /// <param name="userManager">Gestor de usuarios de identidad.</param>
        /// <param name="emailService">Servicio de envío de correos.</param>
        /// <param name="logger">Logger.</param>
        public CompanyConfigController(ApplicationDbContext context, UserManager<IdentityUser> userManager, EmailService emailService, ILogger<CompanyConfigController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }
        #endregion

        #region Métodos de Configuración
        /// <summary>
        /// Muestra la vista de configuración de la empresa.
        /// </summary>
        /// <returns>Vista con los datos de configuración actuales.</returns>
        public async Task<IActionResult> Index()
        {
            var vm = new CompanyConfigViewModel();

            // Obtener usuario actual
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                vm.AdminUserName = currentUser.UserName ?? string.Empty;
                vm.AdminEmail = currentUser.Email ?? string.Empty;
            }

            // Cargar configuración existente
            var config = await _context.CompanyConfigs.FirstOrDefaultAsync();
            if (config != null)
            {
                vm.Id = config.Id;
                vm.CompanyName = config.CompanyName;
                vm.AdditionalEmails = config.AdditionalEmails;

                // Convertir correos adicionales a lista
                if (!string.IsNullOrEmpty(config.AdditionalEmails))
                {
                    vm.EmailList = config.AdditionalEmails.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }

            return View(vm);
        }

        /// <summary>
        /// Guarda la configuración de la empresa.
        /// </summary>
        /// <param name="model">Modelo de configuración de la empresa.</param>
        /// <returns>Vista con el resultado de la operación.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CompanyConfigViewModel model)
        {
            var errores = new List<string>();

            // Validar solo el nombre de la empresa
            if (string.IsNullOrWhiteSpace(model.CompanyName))
            {
                errores.Add("El nombre de la empresa es obligatorio");
                ViewBag.Errores = errores;
                return View(model);
            }

            // Obtener usuario actual
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                model.AdminUserName = currentUser.UserName ?? string.Empty;
                model.AdminEmail = currentUser.Email ?? string.Empty;

                // Actualizar contraseña si se proporcionó (requiere confirmar la contraseña actual,
                // para que no baste con un CSRF/sesión robada para tomar control de la cuenta)
                if (!string.IsNullOrEmpty(model.AdminPassword))
                {
                    if (string.IsNullOrEmpty(model.CurrentPassword) ||
                        !await _userManager.CheckPasswordAsync(currentUser, model.CurrentPassword))
                    {
                        errores.Add("La contraseña actual no es correcta.");
                        ViewBag.Errores = errores;
                        return View(model);
                    }

                    var token = await _userManager.GeneratePasswordResetTokenAsync(currentUser);
                    var result = await _userManager.ResetPasswordAsync(currentUser, token, model.AdminPassword);

                    if (!result.Succeeded)
                    {
                        errores.Add("Error al actualizar la contraseña: " + string.Join(", ", result.Errors.Select(e => e.Description)));
                        ViewBag.Errores = errores;
                        return View(model);
                    }
                }
            }

            // Guardar o actualizar configuración
            var config = await _context.CompanyConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new CompanyConfig
                {
                    CompanyName = model.CompanyName,
                    AdminEmail = model.AdminEmail,
                    AdditionalEmails = model.AdditionalEmails
                };
                _context.CompanyConfigs.Add(config);
            }
            else
            {
                config.CompanyName = model.CompanyName;
                config.AdminEmail = model.AdminEmail;
                config.AdditionalEmails = model.AdditionalEmails;
            }

            await _context.SaveChangesAsync();

            // Convertir correos adicionales a lista para la vista
            if (!string.IsNullOrEmpty(model.AdditionalEmails))
            {
                model.EmailList = model.AdditionalEmails.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            ViewBag.Success = "";
            return View(model);
        }

        /// <summary>
        /// Envía un correo de prueba a los correos proporcionados (sin necesidad de guardar).
        /// </summary>
        /// <param name="request">Lista de correos a los que enviar la prueba.</param>
        /// <returns>Resultado JSON con el estado de la operación.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request)
        {
            try
            {
                // Validar que haya al menos un correo
                if (request.Emails == null || request.Emails.Count == 0)
                {
                    return Json(new { success = false, message = "No hay correos configurados para enviar" });
                }

                var config = await _context.CompanyConfigs.FirstOrDefaultAsync();
                var companyName = config?.CompanyName ?? "Tu Empresa";

                // Enviar correo a cada dirección y llevar la cuenta de fallos
                var failed = new List<string>();
                foreach (var email in request.Emails)
                {
                    if (!string.IsNullOrEmpty(email))
                    {
                        bool sent = await _emailService.SendEmailAsync(
                            email,
                            "Correo de Prueba - BackupPro",
                            $"<h2>Correo de Prueba</h2><p>Este es un correo de prueba enviado desde <strong>BackupPro</strong>.</p><p>Si recibes este mensaje, la configuración de correo está funcionando correctamente.</p><p>Empresa: <strong>{companyName}</strong></p>"
                        );

                        if (!sent)
                        {
                            failed.Add(email);
                        }
                    }
                }

                if (failed.Count > 0)
                {
                    return Json(new { success = false, message = $"No se pudo enviar el correo a: {string.Join(", ", failed)}. Revisa la configuración SMTP." });
                }

                return Json(new { success = true, message = $"Correo de prueba enviado exitosamente a {request.Emails.Count} destinatario(s)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de prueba");
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Clase auxiliar para recibir la lista de correos en SendTestEmail.
        /// </summary>
        public class SendTestEmailRequest
        {
            public List<string> Emails { get; set; } = new List<string>();
        }
        #endregion
    }
}
