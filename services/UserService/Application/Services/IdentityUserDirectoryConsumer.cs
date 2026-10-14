using Collaby.EventBus;
using Collaby.EventBus.Contracts;
using Collaby.EventBus.Internal;
using Collaby.InternalContracts.Events;
using Microsoft.Extensions.Options;
using UserService.Application.Interfaces;

namespace UserService.Application.Services;

public sealed class IdentityUserDirectoryConsumer : KafkaTopicConsumerBackgroundService<IdentityUserDirectoryEventV1>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public IdentityUserDirectoryConsumer(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CollabyKafkaOptions> options,
        IKafkaTopicRegistry topicRegistry,
        KafkaSchemaValidator schemaValidator,
        CollabyKafkaServiceIdentity serviceIdentity,
        KafkaProducerAccessor producerAccessor,
        ILogger<IdentityUserDirectoryConsumer> logger)
        : base(options, topicRegistry, schemaValidator, serviceIdentity, producerAccessor, logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override string Topic => EventTopicNames.IdentityUserDirectoryV1;

    protected override async Task HandleAsync(ConsumedEvent<IdentityUserDirectoryEventV1> message, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var projectionWriter = scope.ServiceProvider.GetRequiredService<IIdentityDirectoryProjectionWriter>();
        await projectionWriter.ApplyAsync([message.Payload], cancellationToken);
    }
}
