using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;

namespace RealtimeHost;

internal sealed class RealtimeMessengerAdminUserDeletionCommandConsumer
    : KafkaTopicConsumerBackgroundService<AdminUserDeletionCommandV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RealtimeMessengerAdminUserDeletionCommandConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<RealtimeMessengerAdminUserDeletionCommandConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionCommandsV1;

    protected override string ConsumerGroup => "messenger-service";

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<MessengerService.Application.Services.AdminUserDeletionPurgeService>();
        await purgeService.HandleAsync(message, cancellationToken);
    }
}

internal sealed class RealtimeCallAdminUserDeletionCommandConsumer
    : KafkaTopicConsumerBackgroundService<AdminUserDeletionCommandV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RealtimeCallAdminUserDeletionCommandConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<RealtimeCallAdminUserDeletionCommandConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionCommandsV1;

    protected override string ConsumerGroup => "call-service";

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<CallService.Application.Services.AdminUserDeletionPurgeService>();
        await purgeService.HandleAsync(message, cancellationToken);
    }
}
