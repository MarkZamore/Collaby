namespace IdentityService.Application.Interfaces;

/// <summary>
/// ЗАГЛУШКА: Two-Factor Authentication interface
/// </summary>
public interface ITwoFactorAuthenticationService
{
    string GenerateTwoFactorSecret();
    Task<string> GenerateTwoFactorQrCodeAsync(string userId, string email);
    bool ValidateTwoFactorCode(string secret, string code);
    Task<bool> EnableTwoFactorAsync(string userId, string verificationCode);
    Task<bool> DisableTwoFactorAsync(string userId, string password);
    string[] GenerateBackupCodes();
}
