using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BackupPro.Data;
using BackupPro.Models;
using BackupPro.Services;
using BackupPro.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador responsable de la autenticación de usuarios (login y logout) y del asistente de
    /// configuración inicial.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        /// <summary>
        /// Inicializa una nueva instancia del <see cref="AccountController"/>.
        /// </summary>
        /// <param name="userManager">Gestor de usuarios de identidad.</param>
        /// <param name="signInManager">Gestor de inicio de sesión de identidad.</param>
        /// <param name="context">Contexto de base de datos.</param>
        /// <param name="logger">Logger.</param>
        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Indica si la petición actual proviene del propio equipo donde corre la aplicación.
        /// </summary>
        private bool IsLocalRequest()
        {
            var remoteIp = HttpContext.Connection.RemoteIpAddress;
            if (remoteIp == null)
            {
                return false;
            }

            if (IPAddress.IsLoopback(remoteIp))
            {
                return true;
            }

            var localIp = HttpContext.Connection.LocalIpAddress;
            return localIp != null && remoteIp.Equals(localIp);
        }

        /// <summary>
        /// Muestra la vista de inicio de sesión.
        /// </summary>
        /// <returns>Vista de Login.</returns>
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        /// <summary>
        /// Procesa el inicio de sesión del usuario.
        /// </summary>
        /// <param name="model">Modelo con los datos de inicio de sesión.</param>
        /// <returns>Redirige al panel principal si es exitoso, de lo contrario retorna la vista de Login con errores.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("login")]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Verificar si no hay usuarios registrados
            var totalUsuarios = _userManager.Users.Count();

            // Si no hay usuarios y se intenta acceder con admin/admin, crear usuario temporal y redirigir a SetupAdmin.
            // Restringido a peticiones desde el propio equipo: como Kestrel escucha en todas las
            // interfaces de red, sin esta restricción cualquiera que llegue a la app antes que el
            // operador complete la configuración inicial podría tomar la cuenta de administrador.
            if (totalUsuarios == 0 && model.Email?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true && model.Password == "admin")
            {
                if (!IsLocalRequest())
                {
                    _logger.LogWarning("Intento de usar el usuario admin/admin de configuración inicial desde una IP no local: {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                    ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");
                    return View(model);
                }

                // Crear usuario admin temporal
                var tempAdmin = new IdentityUser
                {
                    UserName = "admin",
                    Email = "admin@backuppro.local",
                    EmailConfirmed = true
                };

                // Hashear contraseña directamente sin validaciones
                tempAdmin.PasswordHash = _userManager.PasswordHasher.HashPassword(tempAdmin, "admin");
                var createResult = await _userManager.CreateAsync(tempAdmin);
                if (createResult.Succeeded)
                {
                    await _signInManager.SignInAsync(tempAdmin, isPersistent: true);
                    return RedirectToAction(nameof(SetupAdmin));
                }
            }

            // Buscar usuario normal
            IdentityUser? user = null;
            if (!string.IsNullOrWhiteSpace(model.Email) && model.Email.Contains('@'))
            {
                user = await _userManager.FindByEmailAsync(model.Email);
            }
            else
            {
                user = await _userManager.FindByNameAsync(model.Email);
            }

            string errorMessage = "Usuario o contraseña incorrectos.";

            if (user != null)
            {
                // lockoutOnFailure: true para que los intentos fallidos cuenten para el bloqueo
                // temporal configurado en Program.cs (Lockout.MaxFailedAccessAttempts); antes se
                // pasaba false y la cuenta nunca se bloqueaba sin importar cuántos intentos fallaran.
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, true, true);
                if (result.Succeeded)
                {
                    // Si es el usuario por defecto y es el único existente, forzar configuración de admin
                    totalUsuarios = _userManager.Users.Count();
                    if (SetupState.IsBootstrapAdmin(totalUsuarios, user.UserName))
                    {
                        return RedirectToAction(nameof(SetupAdmin));
                    }
                    return RedirectToAction("Index", "Home");
                }

                if (result.IsLockedOut)
                {
                    errorMessage = "Cuenta bloqueada temporalmente por demasiados intentos fallidos. Intenta de nuevo en unos minutos.";
                }
            }

            // Mensaje genérico (salvo bloqueo) para evitar revelar si el usuario existe
            ModelState.AddModelError(string.Empty, errorMessage);

            // Si la petición viene del modal de login (mostrado en páginas distintas a
            // /Account/Login), redirigir de vuelta a esa página para que el modal muestre el error
            // ahí. Se valida con Url.IsLocalUrl para no redirigir a una URL externa arbitraria (el
            // header Referer lo controla quien hace la petición, no es de fiar sin validar), y se
            // comprueba que no esté vacío (curl, clientes API o extensiones de privacidad no
            // siempre lo envían).
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer) && !referer.Contains("/Account"))
            {
                TempData["LoginError"] = errorMessage;
                return Redirect(referer);
            }

            return View(model);
        }

        /// <summary>
        /// Cierra la sesión del usuario actual.
        /// </summary>
        /// <returns>Redirige a la vista de Login.</returns>
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Pantalla para crear/establecer el usuario administrador definitivo al primer inicio.
        /// </summary>
        [Authorize]
        [HttpGet]
        public IActionResult SetupAdmin()
        {
            return View(new SetupAdminViewModel());
        }

        /// <summary>
        /// Crea el usuario admin definitivo y elimina el usuario por defecto (admin).
        /// </summary>
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupAdmin(SetupAdminViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existing = await _userManager.FindByNameAsync(model.UserName);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "El nombre de usuario ya existe.");
                return View(model);
            }

            if (!string.Equals(model.Password, model.ConfirmPassword))
            {
                ModelState.AddModelError(string.Empty, "Las contraseñas no coinciden.");
                return View(model);
            }

            var newUser = new IdentityUser
            {
                UserName = model.UserName,
                Email = model.Email,
                EmailConfirmed = true
            };

            var create = await _userManager.CreateAsync(newUser, model.Password);
            if (!create.Succeeded)
            {
                ModelState.AddModelError(string.Empty, string.Join("; ", create.Errors.Select(e => e.Description)));
                return View(model);
            }

            var defaultUser = await _userManager.FindByNameAsync(SetupState.BootstrapUserName);
            if (defaultUser != null)
            {
                await _userManager.DeleteAsync(defaultUser);
            }

            // Guardar los datos de empresa y correos de notificación en el mismo paso: hasta no
            // completar este formulario, RequireSetupCompleteFilter no deja usar el resto de la
            // app, así que este es el único momento garantizado en el que se le puede pedir esto
            // al usuario. Editable después desde Configuración de la Empresa.
            var config = await _context.CompanyConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                config = new CompanyConfig
                {
                    CompanyName = model.CompanyName,
                    AdminEmail = model.Email,
                    AdditionalEmails = model.AdditionalEmails
                };
                _context.CompanyConfigs.Add(config);
            }
            else
            {
                config.CompanyName = model.CompanyName;
                config.AdminEmail = model.Email;
                config.AdditionalEmails = model.AdditionalEmails;
            }
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(newUser, isPersistent: true);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Inicia el proceso de autenticación externa (Google/Microsoft).
        /// </summary>
        /// <param name="provider">Proveedor de autenticación (Google o Microsoft).</param>
        /// <returns>Redirige al proveedor externo.</returns>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider)
        {
            _logger.LogDebug("ExternalLogin llamado con provider: {Provider}", provider);

            // Solicitar redirección al proveedor externo
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);

            return Challenge(properties, provider);
        }

        /// <summary>
        /// Callback que maneja la respuesta del proveedor externo.
        /// </summary>
        /// <returns>Redirige al Home o a la vista de Login con error.</returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (remoteError != null)
            {
                _logger.LogWarning("Error del proveedor externo de autenticación: {RemoteError}", remoteError);
                TempData["LoginError"] = $"Error del proveedor externo: {remoteError}";
                return RedirectToAction(nameof(Login));
            }

            // Obtener información del login externo
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                _logger.LogWarning("No se pudo obtener información del login externo");
                TempData["LoginError"] = "Error al cargar información del proveedor externo.";
                return RedirectToAction(nameof(Login));
            }

            _logger.LogDebug("Login externo: Provider={Provider}, Email={Email}", info.LoginProvider, info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);

            // Intentar iniciar sesión con el proveedor externo
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

            if (result.Succeeded)
            {
                // Usuario ya existe y se autenticó correctamente
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                TempData["LoginError"] = "Esta cuenta está bloqueada.";
                return RedirectToAction(nameof(Login));
            }

            // El usuario no existe, crear uno nuevo
            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["LoginError"] = "No se pudo obtener el email del proveedor externo.";
                return RedirectToAction(nameof(Login));
            }

            // Verificar si ya existe un usuario con este email
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                // Asociar el login externo al usuario existente
                var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                if (addLoginResult.Succeeded)
                {
                    await _signInManager.SignInAsync(existingUser, isPersistent: true);
                    return RedirectToAction("Index", "Home");
                }
            }

            // Crear nuevo usuario
            var user = new IdentityUser
            {
                UserName = email.Split('@')[0], // Usar parte antes del @ como username
                Email = email,
                EmailConfirmed = true // Asumimos que el email está verificado por el proveedor
            };

            var createResult = await _userManager.CreateAsync(user);
            if (createResult.Succeeded)
            {
                // Asociar el login externo al nuevo usuario
                createResult = await _userManager.AddLoginAsync(user, info);
                if (createResult.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: true);
                    return RedirectToAction("Index", "Home");
                }
            }

            TempData["LoginError"] = "Error al crear la cuenta: " + string.Join(", ", createResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Login));
        }
    }
}
