using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;

namespace AnalyticsService.Application.Services;

public sealed class ManagementTaskEventAnalyticsConsumer : KafkaTopicConsumerBackgroundService<ManagementTaskEventV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ManagementTaskEventAnalyticsConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<ManagementTaskEventAnalyticsConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.ManagementTaskEventsV1;

    protected override async Task HandleAsync(ConsumedEvent<ManagementTaskEventV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();
        await projectionService.IngestManagementTaskAsync(message.Metadata, message.Payload, cancellationToken);
    }
}
