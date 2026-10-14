using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string Token { get; set; } = string.Empty;
    
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(45)]
    public string? CreatedByIp { get; set; }
    
    public DateTime? RevokedAt { get; set; }
    
    [MaxLength(45)]
    public string? RevokedByIp { get; set; }
    
    [MaxLength(500)]
    public string? ReplacedByToken { get; set; }
    
    // Внешний ключ.
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    
    // Вычисляемые свойства.
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
