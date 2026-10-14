using AnalyticsService.Application.Contracts;

namespace AnalyticsService.Application.Interfaces;

public interface IUserLookupClient
{
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
