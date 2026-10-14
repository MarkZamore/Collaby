using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using GamificationService.Contracts.Responses;

namespace GamificationService.Application.Interfaces;

public interface IAnalyticsRewardOutbox
{
    Task EnqueueAsync(
        ConsumedEvent<ManagementTaskEventV1> sourceEvent,
        RewardEventResponse reward,
        CancellationToken cancellationToken = default);
}
