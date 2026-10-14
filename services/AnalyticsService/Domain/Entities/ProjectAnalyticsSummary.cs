namespace AnalyticsService.Domain.Entities;

public sealed class ProjectAnalyticsSummary
{
    public Guid ProjectId { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksReopened { get; set; }
    public int SubtasksCompleted { get; set; }
    public int CommentsAdded { get; set; }
    public long TotalXpDelta { get; set; }
    public long TotalCoinsDelta { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
