namespace IdentityService.Contracts.Requests;

/// <summary>
/// Запрос на подтверждение email по verification token.
/// </summary>
public class VerifyEmailRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на повторную отправку письма подтверждения email.
/// </summary>
public class ResendVerificationEmailRequest
{
    public string Email { get; set; } = string.Empty;
}
