using System.Net;
using System.Net.Mail;

namespace BackupPro.Services
{
    /// <summary>
    /// Servicio para envío de correos electrónicos vía SMTP.
    /// </summary>
    public class EmailService
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailService(SmtpSettings smtpSettings)
        {
            _smtpSettings = smtpSettings;
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
                var host = _smtpSettings.Host;
                var port = _smtpSettings.Port;
                var enableSsl = _smtpSettings.EnableSSL;
                var user = _smtpSettings.Email;
                var password = _smtpSettings.Password;

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                {
                    return;
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
            }
            catch (Exception ex)
            {
            }
        }
    }
}
