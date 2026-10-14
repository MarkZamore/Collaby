using GamificationService.Contracts.Responses;

namespace GamificationService.Application.Interfaces;

public interface IQuestService
{
    Task<IEnumerable<QuestResponse>> GetActiveQuestsAsync(Guid userId);
    Task UpdateProgressAsync(Guid userId, string eventType, string? priority = null);
    Task<RewardEventResponse?> ClaimQuestAsync(Guid userId, Guid questId);
    Task AssignDailyQuestsAsync(Guid userId);
}
