using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using GamificationService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace GamificationService.Application.Services;

public sealed class ManagementProjectProgressSnapshotConsumer : KafkaTopicConsumerBackgroundService<ManagementProjectProgressSnapshotEventV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ManagementProjectProgressSnapshotConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<ManagementProjectProgressSnapshotConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.ManagementProjectProgressSnapshotV1;

    protected override async Task HandleAsync(ConsumedEvent<ManagementProjectProgressSnapshotEventV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var projectionWriter = scope.ServiceProvider.GetRequiredService<IProjectProgressProjectionWriter>();

        if (string.Equals(message.Metadata.EventType, "project-progress-removed", StringComparison.OrdinalIgnoreCase))
        {
            await projectionWriter.RemoveAsync(message.Payload.ProjectId, cancellationToken);
            return;
        }

        await projectionWriter.ApplySnapshotAsync(message.Payload, cancellationToken);
    }
}
