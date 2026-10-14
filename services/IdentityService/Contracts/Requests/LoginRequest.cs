using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts.Requests;

public class LoginRequest
{
    [Required]
    public string EmailOrUsername { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    // Необязательный 2FA-код, если у пользователя включён TOTP.
    public string? TwoFactorCode { get; set; }
}
