namespace Collaby.EventBus;

public sealed record ConsumedEvent<TPayload>(
    string Topic,
    string Key,
    EventMetadata Metadata,
    TPayload Payload);
