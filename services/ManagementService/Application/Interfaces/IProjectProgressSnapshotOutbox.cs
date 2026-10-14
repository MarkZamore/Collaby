namespace ManagementService.Application.Interfaces;

public interface IProjectProgressSnapshotOutbox
{
    Task EnqueueSnapshotAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task EnqueueProjectRemovedAsync(Guid projectId);
}
