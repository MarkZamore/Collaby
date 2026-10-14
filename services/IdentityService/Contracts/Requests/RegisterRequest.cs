using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts.Requests;

public class RegisterRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Nickname { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Необязательный флаг: просим frontend предложить настройку 2FA во время регистрации.
    public bool EnableTwoFactor { get; set; } = false;
}
