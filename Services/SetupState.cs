namespace BackupPro.Services
{
    /// <summary>
    /// Determina si el sistema todavía está en el estado de "recién instalado": el único usuario
    /// existente es la cuenta temporal <c>admin</c> creada por el primer inicio de sesión con
    /// admin/admin (ver la acción "Login" de <see cref="Controllers.AccountController"/>), y
    /// todavía no se completó el asistente de configuración inicial (acción "SetupAdmin").
    /// </summary>
    public static class SetupState
    {
        public const string BootstrapUserName = "admin";

        public static bool IsBootstrapAdmin(int totalUsers, string? userName) =>
            totalUsers == 1 && string.Equals(userName, BootstrapUserName, StringComparison.OrdinalIgnoreCase);
    }
}
