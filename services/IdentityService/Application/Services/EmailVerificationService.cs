using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MimeKit;

namespace IdentityService.Application.Services;

/// <summary>
/// Сервис подтверждения email и восстановления пароля через настраиваемый SMTP.
/// Обрабатывает отправку и проверку email verification tokens
/// </summary>
public class EmailVerificationService : IEmailVerificationService
{
    private readonly IdentityDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailVerificationService> _logger;
    private const int TokenExpirationMinutes = 24 * 60; // 24 часа
    
    public EmailVerificationService(
        IdentityDbContext dbContext,
        IConfiguration configuration,
        ILogger<EmailVerificationService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendVerificationEmailAsync(string email, string userId, string verificationToken)
    {
        try
        {
            _logger.LogInformation("Sending verification email to {Email}", email);
            
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderName = _configuration["Email:SenderName"] ?? "Collaby";

            // Проверить что все настройки заполнены
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("SMTP configuration is incomplete. Email not sent for {Email}.", email);
                return false;
            }

            // Создать email сообщение
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(email, email));
            message.Subject = "Подтверждение адреса электронной почты";

            // Формируем HTML-шаблон письма подтверждения.
            var verificationLink = $"{(_configuration["Frontend:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(verificationToken)}";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ color: #333; margin-bottom: 20px; }}
        .verification-link {{ 
            display: inline-block;
            background-color: #007bff;
            color: white;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px 0;
        }}
        .footer {{ color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2 class='header'>Добро пожаловать в Collaby!</h2>
        <p>Спасибо за регистрацию. Пожалуйста, подтвердите ваш адрес электронной почты, нажав на кнопку ниже:</p>
        
        <a href='{verificationLink}' class='verification-link'>Подтвердить адрес электронной почты</a>
        
        <p>Или скопируйте эту ссылку в браузер:</p>
        <p><code>{verificationLink}</code></p>
        
        <p>Ссылка действительна в течение 24 часов.</p>
        
        <div class='footer'>
            <p>Если вы не регистрировались, просто игнорируйте это письмо.</p>
            <p>&copy; 2026 Collaby. Все права защищены.</p>
        </div>
    </div>
</body>
</html>";

            message.Body = new TextPart("html") { Text = htmlBody };

            // Отправить email через SMTP
            using (var client = new SmtpClient())
            {
                // Подключиться к SMTP серверу
                await client.ConnectAsync(smtpHost, smtpPort, _configuration.GetValue("Email:UseTls", true) ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None);
                
                // Аутентифицироваться
                if (!string.IsNullOrEmpty(smtpUsername)) await client.AuthenticateAsync(smtpUsername, smtpPassword);
                
                // Отправить письмо
                await client.SendAsync(message);
                
                // Отключиться
                await client.DisconnectAsync(true);
            }

            _logger.LogInformation("Verification email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            return false;
        }
    }

    public async Task<(bool Success, string Message, string? Code)> VerifyEmailAsync(string token)
    {
        try
        {
            _logger.LogInformation("Verifying email...");
            
            // Найти verification token в отдельной таблице
            var emailVerification = await _dbContext.EmailVerifications
                .FirstOrDefaultAsync(ev => ev.Token == token);

            if (emailVerification == null)
            {
                _logger.LogWarning("Email verification token not found");
                return (false, "Invalid verification token", "invalid_verification_token");
            }

            if (emailVerification.VerifiedAt.HasValue)
            {
                _logger.LogWarning("Email verification token already used for user {UserId}", emailVerification.UserId);
                return (false, "Verification token has already been used", "verification_token_used");
            }

            // Проверить что token не истёк
            if (DateTime.UtcNow > emailVerification.ExpiresAt)
            {
                _logger.LogWarning("Email verification token expired for user {UserId}", emailVerification.UserId);
                return (false, "Verification token has expired. Please request a new one.", "verification_token_expired");
            }

            // Найти пользователя и установить email как проверенный
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == emailVerification.UserId);
            
            if (user == null)
            {
                _logger.LogWarning("User not found for email verification");
                return (false, "Invalid verification token", "invalid_verification_token");
            }

            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;

            // Помечаем запись подтверждения email как использованную.
            emailVerification.VerifiedAt = DateTime.UtcNow;

            _dbContext.Users.Update(user);
            _dbContext.EmailVerifications.Update(emailVerification);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Email verified successfully for user {UserId}", user.Id);
            
            return (true, "Email verified successfully", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying email");
            return (false, "An error occurred while verifying email", "verification_failed");
        }
    }

    public async Task<(bool Success, string Message, string? Code)> ResendVerificationEmailAsync(string email)
    {
        try
        {
            _logger.LogInformation("Resending verification email to {Email}", email);
            
            // Найти пользователя по email
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                _logger.LogWarning("User not found for email {Email}", email);
                return (false, "User not found", "user_not_found");
            }

            // Если email уже проверен
            if (user.IsEmailVerified)
            {
                _logger.LogInformation("Email already verified for user {UserId}", user.Id);
                return (false, "Email is already verified", "email_already_verified");
            }

            // Инвалидировать все старые verification tokens для этого пользователя
            var oldTokens = await _dbContext.EmailVerifications
                .Where(ev => ev.UserId == user.Id && !ev.VerifiedAt.HasValue)
                .ToListAsync();
            
            if (oldTokens.Any())
            {
                _logger.LogInformation("Invalidating {Count} old verification tokens for user {UserId}", 
                    oldTokens.Count, user.Id);
                
                // Помечаем старые токены как использованные
                foreach (var oldToken in oldTokens)
                {
                    oldToken.VerifiedAt = DateTime.UtcNow;
                }
            }

            // Сгенерировать новый verification token
            var verificationToken = GenerateVerificationToken();
            
            // Создаём новую запись подтверждения email перед повторной отправкой письма.
            var emailVerification = new EmailVerification
            {
                UserId = user.Id,
                Token = verificationToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(TokenExpirationMinutes),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.EmailVerifications.Add(emailVerification);
            await _dbContext.SaveChangesAsync();

            // Отправить email
            await SendVerificationEmailAsync(user.Email, user.Id.ToString(), verificationToken);

            _logger.LogInformation("Verification email resent to {Email}", email);
            return (true, "Verification email sent. Please check your inbox.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email to {Email}", email);
            return (false, "An error occurred while sending verification email", "verification_email_send_failed");
        }
    }

    /// <summary>
    /// Сгенерировать verification token при регистрации
    /// Вызывается из AuthService.RegisterAsync()
    /// </summary>
    public string GenerateVerificationToken()
    {
        return GenerateSecureToken();
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken)
    {
        try
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUsername = _configuration["Email:SmtpUsername"];
            var smtpPassword = _configuration["Email:SmtpPassword"];
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderName = _configuration["Email:SenderName"] ?? "Collaby";

            // Проверить SMTP конфигурацию
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(senderEmail))
            {
                _logger.LogWarning("SMTP configuration is incomplete. Password reset email not sent for {Email}.", email);
                return false;
            }

            // Создать email сообщение
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress(email, email));
            message.Subject = "Восстановление пароля Collaby";

            // Формируем HTML-шаблон письма со ссылкой сброса пароля.
            var resetLink = $"{(_configuration["Frontend:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}&email={Uri.EscapeDataString(email)}";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ color: #333; margin-bottom: 20px; }}
        .reset-link {{ 
            display: inline-block;
            background-color: #dc3545;
            color: white;
            padding: 12px 24px;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px 0;
        }}
        .footer {{ color: #999; font-size: 12px; margin-top: 20px; border-top: 1px solid #eee; padding-top: 20px; }}
        .warning {{ color: #dc3545; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2 class='header'>Восстановление пароля</h2>
        <p>Мы получили запрос на восстановление пароля для вашей учётной записи.</p>
        
        <p>Нажмите на кнопку ниже для установки нового пароля:</p>
        <a href='{resetLink}' class='reset-link'>Восстановить пароль</a>
        
        <p>Или скопируйте эту ссылку в браузер:</p>
        <p><code>{resetLink}</code></p>
        
        <p><span class='warning'>⚠️ Важно:</span> Ссылка действительна в течение <strong>1 часа</strong>.</p>
        
        <p>Если вы не запрашивали восстановление пароля, просто игнорируйте это письмо. Ваш пароль останется без изменений.</p>
        
        <div class='footer'>
            <p>Это автоматическое письмо, пожалуйста, не отвечайте на него.</p>
            <p>&copy; 2026 Collaby. Все права защищены.</p>
        </div>
    </div>
</body>
</html>";

            message.Body = new TextPart("html") { Text = htmlBody };

            // Отправить email через SMTP
            using (var client = new SmtpClient())
            {
                // Подключиться к SMTP серверу
                await client.ConnectAsync(smtpHost, smtpPort, _configuration.GetValue("Email:UseTls", true) ? MailKit.Security.SecureSocketOptions.StartTls : MailKit.Security.SecureSocketOptions.None);
                
                // Аутентифицироваться
                if (!string.IsNullOrEmpty(smtpUsername)) await client.AuthenticateAsync(smtpUsername, smtpPassword);
                
                // Отправить письмо
                await client.SendAsync(message);
                
                // Отключиться
                await client.DisconnectAsync(true);
            }

            _logger.LogInformation("Password reset email sent successfully to {Email}", email);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            return false;
        }
    }

    private static string GenerateSecureToken(int byteLength = 32)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));
    }
}


