using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Enums;

namespace GamificationService.Application.Interfaces;

public interface IGamificationEventJournal
{
    Task AppendAsync(
        ConsumedEvent<ManagementTaskEventV1> request,
        XpReason? reason,
        RewardEventResponse? reward,
        CancellationToken cancellationToken = default);
}
