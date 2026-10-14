namespace AnalyticsService.Domain.Entities;

public sealed class AnalyticsInboxEvent
{
    public Guid EventId { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Metadata { get; set; }
    public long? XpDelta { get; set; }
    public long? CoinsDelta { get; set; }
    public int? LevelAfterEvent { get; set; }
    public bool IsProjectionSupported { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
