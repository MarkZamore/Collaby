namespace GamificationService.Domain.Entities;

public class UserGlobalXp
{
    public Guid UserId { get; set; }
    public long TotalXp { get; set; }
    public int Level { get; set; } = 1;
    public long Coins { get; set; }
    public int StreakDays { get; set; }
    public DateTime? LastActiveDate { get; set; }
    public int FreezeCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
