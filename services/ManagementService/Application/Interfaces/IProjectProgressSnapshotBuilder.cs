using ManagementService.Contracts.Responses;

namespace ManagementService.Application.Interfaces;

public interface IProjectProgressSnapshotBuilder
{
    Task<ProjectProgressResponse?> BuildAsync(Guid projectId, CancellationToken cancellationToken = default);
}
