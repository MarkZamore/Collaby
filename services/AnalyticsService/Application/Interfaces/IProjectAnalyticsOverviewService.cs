using AnalyticsService.Application.Contracts;

namespace AnalyticsService.Application.Interfaces;

public interface IProjectAnalyticsOverviewService
{
    Task<ProjectAnalyticsOverviewDto> BuildOverviewAsync(
        Guid projectId,
        ManagementProjectAnalyticsSnapshot snapshot,
        ProjectAnalyticsPeriod period,
        CancellationToken cancellationToken = default);
}
