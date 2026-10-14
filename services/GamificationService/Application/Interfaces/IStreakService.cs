using GamificationService.Contracts.Responses;

namespace GamificationService.Application.Interfaces;

public interface IStreakService
{
    Task RecordActivityAsync(Guid userId);
    Task<StreakResponse> GetStreakAsync(Guid userId);
    Task<bool> UseFreezeAsync(Guid userId);
    double GetStreakMultiplier(int streakDays);
}
