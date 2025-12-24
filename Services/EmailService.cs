using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace MovieApi.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            var host = _config["Smtp:Host"];
            var port = int.Parse(_config["Smtp:Port"]!);
            var username = _config["Smtp:Username"];
            var appPassword = _config["Smtp:AppPassword"];
            var fromEmail = _config["Smtp:FromEmail"];
            var fromName = _config["Smtp:FromName"];

            using var client = new SmtpClient(host!, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, appPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
    }
}
