using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;

namespace GamificationService.Application.Services;

public sealed class ManagementTaskEventsConsumer : KafkaTopicConsumerBackgroundService<ManagementTaskEventV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ManagementTaskEventsConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<ManagementTaskEventsConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.ManagementTaskEventsV1;

    protected override async Task HandleAsync(ConsumedEvent<ManagementTaskEventV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<ManagementTaskEventProcessor>();
        await processor.ProcessAsync(message, cancellationToken);
    }
}
