using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace BackupPro.Services
{
    /// <summary>
    /// Servicio para envío de correos electrónicos vía SMTP.
    /// </summary>
    public class EmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(SmtpSettings smtpSettings, ILogger<EmailService> logger)
        {
            _smtpSettings = smtpSettings;
            _logger = logger;
        }

        /// <summary>
        /// Envía un correo electrónico HTML.
        /// </summary>
        /// <param name="to">Destino del correo.</param>
        /// <param name="subject">Asunto del mensaje.</param>
        /// <param name="body">Cuerpo en HTML.</param>
        /// <returns><c>true</c> si el correo se envió correctamente; <c>false</c> en caso contrario.</returns>
        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                var host = _smtpSettings.Host;
                var port = _smtpSettings.Port;
                var enableSsl = _smtpSettings.EnableSSL;
                var user = _smtpSettings.Email;
                var password = _smtpSettings.Password;

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                {
                    _logger.LogWarning("No se puede enviar el correo a {To}: la configuración SMTP (Host/Email) está incompleta.", to);
                    return false;
                }

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
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo a {To}", to);
                return false;
            }
        }
    }
}
