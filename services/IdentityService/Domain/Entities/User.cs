using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
    
    public Domain.Enums.AuthProvider Provider { get; set; } = Domain.Enums.AuthProvider.Local;
    
    public bool IsEmailVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    
    // Навигационные свойства.
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();
    public virtual ICollection<PasswordReset> PasswordResets { get; set; } = new List<PasswordReset>();
    public virtual ICollection<OAuthAccount> OAuthAccounts { get; set; } = new List<OAuthAccount>();
    public virtual TwoFactorSetting? TwoFactorSetting { get; set; }
}
