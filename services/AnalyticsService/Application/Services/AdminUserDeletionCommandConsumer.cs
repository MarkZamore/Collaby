using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;

namespace AnalyticsService.Application.Services;

public sealed class AdminUserDeletionCommandConsumer : KafkaTopicConsumerBackgroundService<AdminUserDeletionCommandV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public AdminUserDeletionCommandConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<AdminUserDeletionCommandConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionCommandsV1;

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<AdminUserDeletionPurgeService>();
        await purgeService.HandleAsync(message, cancellationToken);
    }
}
