namespace GamificationService.Domain.Entities;

public sealed class ProcessedIntegrationEvent
{
    public Guid Id { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ResponseJson { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}
