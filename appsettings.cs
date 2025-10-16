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
    /// Configuración de SMTP para envío de correos
    /// TODO: En producción, encriptar estos valores
    /// </summary>
    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool EnableSSL { get; set; } = true;
        public string Email { get; set; } = "ismaelruge@gmail.com";
        public string Password { get; set; } = "hpxd ysty yoxm flsw";
    }

    /// <summary>
    /// Configuración de Google OAuth para Google Drive
    /// TODO: En producción, almacenar en Azure Key Vault o similar
    /// </summary>
    public class GoogleOAuthSettings
    {
        public string ClientId { get; set; } = "755574195575-srs84155tqd4r9t6lle8gnadllkde180.apps.googleusercontent.com";
        public string ClientSecret { get; set; } = "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";
        public string RedirectUri { get; set; } = "http://localhost:5070/GoogleDriveStorage/GoogleDriveCallback";
    }

    /// <summary>
    /// Configuración de OneDrive OAuth
    /// TODO: En producción, almacenar en Azure Key Vault o similar
    /// </summary>
    public class OneDriveSettings
    {
        public string ClientId { get; set; } = "d10f66e3-b422-4402-9735-d58d8a462a9b";
        public string ClientSecret { get; set; } = "YqF8Q~PgQpHktbNxlhCiRCP7E1S.5S8~KuGpYadU";
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
        public string SecureToken { get; set; } = "TuTokenSeguro123";
        public bool UseSystemScheduler { get; set; } = false;
        public int IntervalMinutes { get; set; } = 60;
    }
}
