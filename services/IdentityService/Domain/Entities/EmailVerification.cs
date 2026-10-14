using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

/// <summary>
/// Таблица для email верификации при регистрации
/// Временные данные - удаляются после верификации
/// </summary>
public class EmailVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public DateTime? VerifiedAt { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационное свойство.
    public virtual User User { get; set; } = null!;
}
