using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ELearningPlatform.Core.Interfaces;

namespace ELearningPlatform.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _config.GetSection("EmailSettings");

            // قراءة القيم بأسماء مطابقة تماماً لـ appsettings.json ومعالجة الـ Null
            var host = emailSettings["Host"] ?? "smtp.gmail.com";
            var port = int.Parse(emailSettings["Port"] ?? "587");
            var username = emailSettings["Username"] ?? string.Empty;
            var password = emailSettings["Password"] ?? string.Empty;
            var senderEmail = emailSettings["SenderEmail"] ?? string.Empty;

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Fluency First"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}