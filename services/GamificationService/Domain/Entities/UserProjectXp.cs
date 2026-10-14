namespace GamificationService.Domain.Entities;

public class UserProjectXp
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public long Xp { get; set; }
    public int Level { get; set; } = 1;
    public int TasksCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
