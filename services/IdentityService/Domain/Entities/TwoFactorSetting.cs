using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

/// <summary>
/// Таблица для настроек двухфакторной аутентификации
/// Опциональные данные - только для юзеров, включивших 2FA
/// </summary>
public class TwoFactorSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Secret { get; set; } = string.Empty;
    
    [Required]
    public bool Enabled { get; set; } = false;
    
    public string[]? BackupCodes { get; set; }
    
    public DateTime? EnabledAt { get; set; }
    
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационное свойство.
    public virtual User User { get; set; } = null!;
}
