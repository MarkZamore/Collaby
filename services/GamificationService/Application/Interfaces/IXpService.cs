using GamificationService.Contracts.Responses;
using GamificationService.Domain.Enums;

namespace GamificationService.Application.Interfaces;

public interface IXpService
{
    Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, XpReason reason, Guid? taskId = null, string? priority = null);
    Task<RewardEventResponse> RollbackXpAsync(Guid userId, Guid? projectId, Guid taskId);
    Task<UserXpResponse> GetUserGlobalXpAsync(Guid userId);
    Task<UserProjectXpResponse> GetUserProjectXpAsync(Guid userId, Guid projectId);
    Task<IEnumerable<XpHistoryItemResponse>> GetXpHistoryAsync(Guid userId, int limit = 50, int offset = 0);
    Task EnsureUserExistsAsync(Guid userId);
}
