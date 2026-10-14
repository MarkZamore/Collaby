using AnalyticsService.Application.Contracts;

namespace AnalyticsService.Application.Interfaces;

public interface IManagementProjectAnalyticsSnapshotClient
{
    Task<ManagementProjectAnalyticsSnapshot?> GetProjectAnalyticsSnapshotAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
