using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;

namespace WorkspaceHost;

internal sealed class WorkspaceManagementAdminUserDeletionCommandConsumer
    : KafkaTopicConsumerBackgroundService<AdminUserDeletionCommandV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WorkspaceManagementAdminUserDeletionCommandConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<WorkspaceManagementAdminUserDeletionCommandConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionCommandsV1;

    protected override string ConsumerGroup => "management-service";

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<ManagementService.Application.Services.AdminUserDeletionPurgeService>();
        await purgeService.HandleAsync(message, cancellationToken);
    }
}

internal sealed class WorkspaceAnalyticsAdminUserDeletionCommandConsumer
    : KafkaTopicConsumerBackgroundService<AdminUserDeletionCommandV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public WorkspaceAnalyticsAdminUserDeletionCommandConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<WorkspaceAnalyticsAdminUserDeletionCommandConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionCommandsV1;

    protected override string ConsumerGroup => "analytics-service";

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var purgeService = scope.ServiceProvider.GetRequiredService<AnalyticsService.Application.Services.AdminUserDeletionPurgeService>();
        await purgeService.HandleAsync(message, cancellationToken);
    }
}
