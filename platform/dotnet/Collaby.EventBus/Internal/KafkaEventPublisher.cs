using System.Text;
using System.Text.Json;
using Collaby.EventBus.Abstractions;
using Collaby.EventBus.Contracts;
using Collaby.ServiceDefaults.Correlation;
using Confluent.Kafka;

namespace Collaby.EventBus.Internal;

internal sealed class KafkaEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CollabyKafkaServiceIdentity _serviceIdentity;
    private readonly IKafkaTopicRegistry _topicRegistry;
    private readonly KafkaSchemaValidator _schemaValidator;
    private readonly KafkaProducerAccessor _producerAccessor;

    public KafkaEventPublisher(
        CollabyKafkaServiceIdentity serviceIdentity,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        KafkaProducerAccessor producerAccessor)
    {
        _serviceIdentity = serviceIdentity;
        _topicRegistry = topicRegistry;
        _schemaValidator = schemaValidator;
        _producerAccessor = producerAccessor;
    }

    public async Task PublishAsync<TPayload>(EventPublishRequest<TPayload> request, CancellationToken cancellationToken = default)
    {
        var topicContract = _topicRegistry.GetRequiredTopic(request.Topic);
        var payloadJson = JsonSerializer.Serialize(request.Payload, JsonOptions);
        _schemaValidator.ValidatePayload(topicContract, payloadJson);

        var eventId = request.EventId ?? Guid.NewGuid();
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? CorrelationContext.GetOrCreate()
            : request.CorrelationId;
        var causationId = string.IsNullOrWhiteSpace(request.CausationId)
            ? correlationId
            : request.CausationId;
        var occurredAtUtc = request.OccurredAtUtc ?? DateTime.UtcNow;

        var headers = new Headers
        {
            { KafkaEventHeaderNames.EventId, Encoding.UTF8.GetBytes(eventId.ToString()) },
            { KafkaEventHeaderNames.EventType, Encoding.UTF8.GetBytes(request.EventType) },
            { KafkaEventHeaderNames.SchemaVersion, Encoding.UTF8.GetBytes(topicContract.SchemaVersion) },
            { KafkaEventHeaderNames.SourceService, Encoding.UTF8.GetBytes(_serviceIdentity.ServiceName) },
            { KafkaEventHeaderNames.CorrelationId, Encoding.UTF8.GetBytes(correlationId) },
            { KafkaEventHeaderNames.CausationId, Encoding.UTF8.GetBytes(causationId) },
            { KafkaEventHeaderNames.OccurredAtUtc, Encoding.UTF8.GetBytes(occurredAtUtc.ToString("O")) }
        };

        await _producerAccessor.Producer.ProduceAsync(
            request.Topic,
            new Message<string, string>
            {
                Key = request.Key,
                Value = payloadJson,
                Headers = headers
            },
            cancellationToken);
    }
}
