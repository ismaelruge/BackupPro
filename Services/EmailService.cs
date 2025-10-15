using System.Net;
using System.Net.Mail;

namespace BackupPro.Services
{
    /// <summary>
    /// Servicio para envío de correos electrónicos vía SMTP.
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Envía un correo electrónico HTML.
        /// </summary>
        /// <param name="to">Destino del correo.</param>
        /// <param name="subject">Asunto del mensaje.</param>
        /// <param name="body">Cuerpo en HTML.</param>
        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var host = _config["Smtp:Host"];
                var portStr = _config["Smtp:Port"];
                var enableSslStr = _config["Smtp:EnableSSL"];
                var user = _config["Smtp:Email"];
                var password = _config["Smtp:Password"];

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                {
                    return;
                }

                int port = int.TryParse(portStr, out var p) ? p : 25;
                bool enableSsl = bool.TryParse(enableSslStr, out var s) ? s : true;

                using var smtp = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, password)
                };

                using var mail = new MailMessage(user, to, subject, body)
                {
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
