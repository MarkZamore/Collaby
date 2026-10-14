using GamificationService.Contracts.Responses;

namespace GamificationService.Application.Interfaces;

public interface ILeaderboardService
{
    Task<IEnumerable<LeaderboardEntryResponse>> GetGlobalTopAsync(int count = 10);
    Task<IEnumerable<LeaderboardEntryResponse>> GetProjectTopAsync(Guid projectId, int count = 10);
    Task RefreshGlobalLeaderboardAsync();
    Task RefreshProjectLeaderboardAsync(Guid projectId);
    Task CreateAnnualSnapshotAsync(int year);
}
