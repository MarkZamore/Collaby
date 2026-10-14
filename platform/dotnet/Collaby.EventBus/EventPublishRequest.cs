namespace Collaby.EventBus;

public sealed record EventPublishRequest<TPayload>(
    string Topic,
    string Key,
    string EventType,
    TPayload Payload)
{
    public Guid? EventId { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public DateTime? OccurredAtUtc { get; init; }
}
