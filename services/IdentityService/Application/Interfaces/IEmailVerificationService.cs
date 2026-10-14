namespace IdentityService.Application.Interfaces;

/// <summary>
/// Контракт сервиса подтверждения email.
/// Управляет отправкой и проверкой email verification tokens
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>
    /// Отправить email с ссылкой на подтверждение
    /// </summary>
    Task<bool> SendVerificationEmailAsync(string email, string userId, string verificationToken);

    /// <summary>
    /// Проверить email используя verification token
    /// </summary>
    Task<(bool Success, string Message, string? Code)> VerifyEmailAsync(string token);

    /// <summary>
    /// Переотправить email если пользователь его не получил
    /// </summary>
    Task<(bool Success, string Message, string? Code)> ResendVerificationEmailAsync(string email);

    /// <summary>
    /// Отправить email для восстановления пароля
    /// </summary>
    Task<bool> SendPasswordResetEmailAsync(string email, string resetToken);

    /// <summary>
    /// Сгенерировать новый verification token
    /// </summary>
    string GenerateVerificationToken();
}
