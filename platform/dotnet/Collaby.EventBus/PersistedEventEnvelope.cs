namespace Collaby.EventBus;

public sealed record PersistedEventEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    string CorrelationId,
    string CausationId,
    DateTime OccurredAtUtc,
    TPayload Payload);
