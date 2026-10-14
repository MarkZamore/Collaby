namespace Collaby.EventBus;

public sealed record EventMetadata(
    Guid EventId,
    string EventType,
    string SchemaVersion,
    string SourceService,
    string CorrelationId,
    string CausationId,
    DateTime OccurredAtUtc);
