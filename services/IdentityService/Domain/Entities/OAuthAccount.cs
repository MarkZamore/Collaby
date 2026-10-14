using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

/// <summary>
/// Таблица для OAuth аккаунтов (Google, GitHub и т.д.)
/// Опциональные данные - только для юзеров, прошедших через OAuth
/// </summary>
public class OAuthAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public Domain.Enums.AuthProvider Provider { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string ProviderId { get; set; } = string.Empty;
    
    [Required]
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastUsedAt { get; set; }
    
    // Навигационное свойство.
    public virtual User User { get; set; } = null!;
}
