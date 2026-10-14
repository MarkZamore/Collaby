namespace Collaby.EventBus.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(EventPublishRequest<TPayload> request, CancellationToken cancellationToken = default);
}
