namespace GamificationService.Domain.Entities;

public class UserQuest
{
    public Guid UserId { get; set; }
    public Guid QuestId { get; set; }
    public string WindowKey { get; set; } = string.Empty;
    public int Progress { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ClaimedAt { get; set; }

    public virtual Quest? Quest { get; set; }
}
