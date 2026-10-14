namespace IdentityService.Contracts.Requests;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Запрос на отправку письма для сброса пароля.
/// </summary>
public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на сброс пароля по токену.
/// </summary>
public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Запрос на валидацию токена сброса пароля до отображения формы.
/// </summary>
public class ValidateResetPasswordTokenRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// ЗАГЛУШКА: Request for changing current password
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// ЗАГЛУШКА: Request for Google OAuth sign-in
/// </summary>
public class GoogleOAuthRequest
{
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>
/// ЗАГЛУШКА: Request for enabling 2FA
/// </summary>
public class Enable2FARequest
{
    public string VerificationCode { get; set; } = string.Empty;
}

/// <summary>
/// ЗАГЛУШКА: Request for disabling 2FA
/// </summary>
public class Disable2FARequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// ЗАГЛУШКА: Request for validating 2FA code during login
/// </summary>
public class Verify2FACodeRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
