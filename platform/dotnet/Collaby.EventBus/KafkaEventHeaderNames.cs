namespace Collaby.EventBus;

public static class KafkaEventHeaderNames
{
    public const string EventId = "eventId";
    public const string EventType = "eventType";
    public const string SchemaVersion = "schemaVersion";
    public const string SourceService = "sourceService";
    public const string CorrelationId = "correlationId";
    public const string CausationId = "causationId";
    public const string OccurredAtUtc = "occurredAtUtc";
    public const string DlqReason = "dlqReason";
}
