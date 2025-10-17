using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BackupPro.ViewModels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace BackupPro.Controllers
{
    /// <summary>
    /// Controlador responsable de la autenticación de usuarios (login y logout).
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        /// <summary>
        /// Inicializa una nueva instancia del <see cref="AccountController"/>.
        /// </summary>
        /// <param name="userManager">Gestor de usuarios de identidad.</param>
        /// <param name="signInManager">Gestor de inicio de sesión de identidad.</param>
        public AccountController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Verificar si no hay usuarios registrados
            var totalUsuarios = _userManager.Users.Count();

            // Si no hay usuarios y se intenta acceder con admin/admin, crear usuario temporal y redirigir a SetupAdmin
            if (totalUsuarios == 0 && model.Email?.Equals("admin", StringComparison.OrdinalIgnoreCase) == true && model.Password == "admin")
            {
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

            if (user != null)
            {
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, true, false);
                if (result.Succeeded)
                {
                    // Si es el usuario por defecto y es el único existente, forzar configuración de admin
                    totalUsuarios = _userManager.Users.Count();
                    if ((user.UserName?.Equals("admin", StringComparison.OrdinalIgnoreCase) ?? false) && totalUsuarios == 1)
                    {
                        return RedirectToAction(nameof(SetupAdmin));
                    }
                    return RedirectToAction("Index", "Home");
                }
            }

            // Mensaje genérico para evitar revelar si el usuario existe
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectos.");

            // Si la petición viene del modal (tiene Referer), redirigir con el error
            if (Request.Headers["Referer"].ToString().Contains("/Home") ||
                Request.Headers["Referer"].ToString().Contains("/Account") == false)
            {
                TempData["LoginError"] = "Usuario o contraseña incorrectos.";
                return Redirect(Request.Headers["Referer"].ToString());
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

            var defaultUser = await _userManager.FindByNameAsync("admin");
            if (defaultUser != null)
            {
                await _userManager.DeleteAsync(defaultUser);
            }

            await _signInManager.SignInAsync(newUser, isPersistent: true);
            return RedirectToAction("Index", "Home");
        }
    }
}
