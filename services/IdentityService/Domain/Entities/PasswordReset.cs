using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

/// <summary>
/// Таблица для восстановления пароля
/// Временные данные - удаляются после использования
/// </summary>
public class PasswordReset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    public DateTime ExpiresAt { get; set; }
    
    public DateTime? UsedAt { get; set; }
    
    [MaxLength(45)]
    public string? CreatedByIp { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационное свойство.
    public virtual User User { get; set; } = null!;
}
