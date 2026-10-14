using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using IdentityService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class UserDeletionResultsConsumer : KafkaTopicConsumerBackgroundService<AdminUserDeletionResultV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public UserDeletionResultsConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<UserDeletionResultsConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.AdminUserDeletionResultsV1;

    protected override async Task HandleAsync(ConsumedEvent<AdminUserDeletionResultV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IUserDeletionOrchestrator>();
        await orchestrator.ApplyDeletionResultAsync(message.Metadata, message.Payload, cancellationToken);
    }
}
