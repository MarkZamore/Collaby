namespace AnalyticsService.Domain.Entities;

public sealed class UserAnalyticsSummary
{
    public Guid UserId { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksReopened { get; set; }
    public int SubtasksCompleted { get; set; }
    public int CommentsAdded { get; set; }
    public long TotalXpDelta { get; set; }
    public long TotalCoinsDelta { get; set; }
    public int? CurrentLevel { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
