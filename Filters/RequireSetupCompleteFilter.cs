using BackupPro.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace BackupPro.Filters
{
    /// <summary>
    /// Mientras el sistema esté en el estado de "recién instalado" (ver <see cref="SetupState"/>),
    /// obliga a completar el asistente de configuración inicial (acción "SetupAdmin" de
    /// <see cref="Controllers.AccountController"/>) antes de poder usar cualquier otra parte de la
    /// aplicación. Sin este filtro, el usuario admin temporal podía navegar directamente a otra
    /// pantalla sin cambiar nunca la contraseña por defecto.
    /// </summary>
    public class RequireSetupCompleteFilter : IAsyncActionFilter
    {
        private readonly UserManager<IdentityUser> _userManager;

        public RequireSetupCompleteFilter(UserManager<IdentityUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true || IsSetupAllowedAction(context))
            {
                await next();
                return;
            }

            int totalUsers = await _userManager.Users.CountAsync();
            if (SetupState.IsBootstrapAdmin(totalUsers, context.HttpContext.User.Identity.Name))
            {
                context.Result = new RedirectToActionResult("SetupAdmin", "Account", null);
                return;
            }

            await next();
        }

        private static bool IsSetupAllowedAction(ActionExecutingContext context)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            if (!string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var action = context.RouteData.Values["action"]?.ToString();
            return action is "SetupAdmin" or "Logout" or "Login" or "ExternalLogin" or "ExternalLoginCallback";
        }
    }
}
