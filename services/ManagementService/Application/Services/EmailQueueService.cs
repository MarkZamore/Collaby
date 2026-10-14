using System.Net;
using System.Net.Mail;
using ManagementService.Application.Events;
using ManagementService.Application.Interfaces;

namespace ManagementService.Application.Services;

public class EmailQueueService(ILogger<EmailQueueService> logger, IConfiguration configuration) : IEmailQueueService
{
    public async Task QueueInvitationEmailAsync(InvitationEmailEvent emailEvent)
    {
        var baseUrl = (configuration["Frontend:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');
        var link = $"{baseUrl}/accept-invitation?token={Uri.EscapeDataString(emailEvent.InvitationToken)}";
        using var message = new MailMessage(configuration["Email:SenderEmail"] ?? "noreply@collaby.local", emailEvent.Email)
        {
            Subject = $"Приглашение в {emailEvent.WorkspaceName}",
            Body = $"{emailEvent.InviterName} приглашает вас в {emailEvent.WorkspaceName}.\nРоль: {emailEvent.Role}\n{link}\nДействует до {emailEvent.ExpiresAt:O}"
        };
        using var smtp = new SmtpClient(configuration["Email:SmtpHost"] ?? "mailpit", configuration.GetValue("Email:SmtpPort", 1025));
        smtp.EnableSsl = configuration.GetValue("Email:UseTls", false);
        if (!string.IsNullOrWhiteSpace(configuration["Email:SmtpUsername"])) smtp.Credentials = new NetworkCredential(configuration["Email:SmtpUsername"], configuration["Email:SmtpPassword"]);
        await smtp.SendMailAsync(message);
        logger.LogInformation("Письмо с приглашением отправлено в {Email}", emailEvent.Email);
    }
}
