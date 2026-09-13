namespace BackupPro
{
    /// <summary>
    /// Configuración principal de la aplicación
    /// </summary>
    public class AppSettings
    {
        public SerilogSettings Serilog { get; set; } = new();
        public LoggingSettings Logging { get; set; } = new();
        public string AllowedHosts { get; set; } = "*";
        public SmtpSettings Smtp { get; set; } = new();
        public GoogleOAuthSettings GoogleOAuth { get; set; } = new();
        public OneDriveSettings OneDrive { get; set; } = new();
        public SchedulerSettings Scheduler { get; set; } = new();
    }

    /// <summary>
    /// Configuración de Serilog
    /// </summary>
    public class SerilogSettings
    {
        public string[] Using { get; set; } = Array.Empty<string>();
        public MinimumLevelSettings MinimumLevel { get; set; } = new();
    }

    public class MinimumLevelSettings
    {
        public string Default { get; set; } = "Fatal";
        public Dictionary<string, string> Override { get; set; } = new()
        {
            { "Microsoft.AspNetCore", "Fatal" }
        };
    }

    /// <summary>
    /// Configuración de Logging
    /// </summary>
    public class LoggingSettings
    {
        public Dictionary<string, string> LogLevel { get; set; } = new()
        {
            { "Default", "Error" },
            { "Microsoft.AspNetCore", "Error" }
        };
    }

    /// <summary>
    /// Configuración de SMTP para envío de correos.
    /// Los valores reales se cargan desde appsettings.json / variables de entorno
    /// (p.ej. Smtp__Password); nunca deben hardcodearse aquí. Ver appsettings.Example.json.
    /// </summary>
    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSSL { get; set; } = true;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuración de Google OAuth para Google Drive.
    /// Los valores reales se cargan desde appsettings.json / variables de entorno
    /// (p.ej. GoogleOAuth__ClientSecret); nunca deben hardcodearse aquí. Ver appsettings.Example.json.
    /// </summary>
    public class GoogleOAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://localhost:5070/GoogleDriveStorage/GoogleDriveCallback";
    }

    /// <summary>
    /// Configuración de OneDrive OAuth.
    /// Los valores reales se cargan desde appsettings.json / variables de entorno
    /// (p.ej. OneDrive__ClientSecret); nunca deben hardcodearse aquí. Ver appsettings.Example.json.
    /// </summary>
    public class OneDriveSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://localhost:5070/OneDriveStorage/OneDriveCallback";
        public string Authority { get; set; } = "https://login.microsoftonline.com/common";
        public string AuthorizeUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        public string TokenUrl { get; set; } = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        public string[] Scopes { get; set; } = new[]
        {
            "openid",
            "offline_access",
            "profile",
            "Files.ReadWrite",
            "Sites.ReadWrite.All"
        };
        public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
    }

    /// <summary>
    /// Configuración del Scheduler para tareas programadas
    /// </summary>
    public class SchedulerSettings
    {
        public string SecureToken { get; set; } = string.Empty;
        public bool UseSystemScheduler { get; set; } = false;
        public int IntervalMinutes { get; set; } = 60;
    }
}
