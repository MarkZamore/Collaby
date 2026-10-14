namespace GamificationService.Application.Interfaces;

public interface IProjectAuthorizationClient
{
    Task<ProjectAuthorizationResult> GetProjectAuthorizationAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record ProjectAuthorizationResult(Guid ProjectId, Guid WorkspaceId, bool IsOwnerOrAdmin);
