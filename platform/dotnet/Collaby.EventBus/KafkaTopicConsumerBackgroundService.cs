using System.Text;
using System.Text.Json;
using Collaby.EventBus.Abstractions;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Collaby.EventBus;

public abstract class KafkaTopicConsumerBackgroundService<TPayload> : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IOptions<CollabyKafkaOptions> _options;
    private readonly IKafkaTopicRegistry _topicRegistry;
    private readonly KafkaSchemaValidator _schemaValidator;
    private readonly CollabyKafkaServiceIdentity _serviceIdentity;
    private readonly KafkaProducerAccessor _producerAccessor;
    private readonly ILogger _logger;

    protected KafkaTopicConsumerBackgroundService(
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger logger)
    {
        _options = options;
        _topicRegistry = topicRegistry;
        _schemaValidator = schemaValidator;
        _serviceIdentity = serviceIdentity;
        _producerAccessor = producerAccessor;
        _logger = logger;
    }

    protected abstract string Topic { get; }

    protected virtual string ConsumerGroup => _serviceIdentity.ServiceName;

    protected abstract Task HandleAsync(ConsumedEvent<TPayload> message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var contract = _topicRegistry.GetRequiredTopic(Topic);
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.Value.BootstrapServers,
            GroupId = ConsumerGroup,
            ClientId = $"{_serviceIdentity.ServiceName}-{Topic}-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromSeconds(1));
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume failed for topic {Topic}.", Topic);
                continue;
            }

            if (result is null)
            {
                continue;
            }

            try
            {
                _schemaValidator.ValidatePayload(contract, result.Message.Value);
                var payload = JsonSerializer.Deserialize<TPayload>(result.Message.Value, JsonOptions)
                    ?? throw new InvalidOperationException($"Kafka payload for topic {Topic} must not deserialize to null.");
                var metadata = ParseHeaders(result.Message.Headers);
                var message = new ConsumedEvent<TPayload>(Topic, result.Message.Key, metadata, payload);

                await HandleAsync(message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kafka message handling failed for topic {Topic}. Publishing to DLQ.", Topic);
                await PublishToDlqAsync(contract, result, ex.Message, stoppingToken);
            }
            finally
            {
                consumer.Commit(result);
            }
        }
    }

    private async Task PublishToDlqAsync(
        KafkaTopicContract contract,
        ConsumeResult<string, string> result,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var dlqHeaders = new Headers();
        foreach (var header in result.Message.Headers)
        {
            dlqHeaders.Add(header.Key, header.GetValueBytes());
        }

        dlqHeaders.Remove(KafkaEventHeaderNames.DlqReason);
        dlqHeaders.Add(KafkaEventHeaderNames.DlqReason, Encoding.UTF8.GetBytes(failureReason));

        await _producerAccessor.Producer.ProduceAsync(
            contract.DlqTopic,
            new Message<string, string>
            {
                Key = result.Message.Key,
                Value = result.Message.Value,
                Headers = dlqHeaders
            },
            cancellationToken);
    }

    private static EventMetadata ParseHeaders(Headers headers)
    {
        var values = headers.ToDictionary(
            header => header.Key,
            header => Encoding.UTF8.GetString(header.GetValueBytes()),
            StringComparer.Ordinal);

        return new EventMetadata(
            Guid.Parse(values[KafkaEventHeaderNames.EventId]),
            values[KafkaEventHeaderNames.EventType],
            values[KafkaEventHeaderNames.SchemaVersion],
            values[KafkaEventHeaderNames.SourceService],
            values[KafkaEventHeaderNames.CorrelationId],
            values[KafkaEventHeaderNames.CausationId],
            DateTime.Parse(values[KafkaEventHeaderNames.OccurredAtUtc], null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}
