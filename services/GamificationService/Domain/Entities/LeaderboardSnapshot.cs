using GamificationService.Domain.Enums;

namespace GamificationService.Domain.Entities;

public class LeaderboardSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LeaderboardType Type { get; set; }
    public Guid? ProjectId { get; set; }
    public int Year { get; set; }
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public long Xp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
