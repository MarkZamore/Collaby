using Collaby.InternalContracts.Events;

namespace GamificationService.Application.Interfaces;

public interface IProjectProgressProjectionWriter
{
    Task ApplySnapshotAsync(
        ManagementProjectProgressSnapshotEventV1 snapshot,
        CancellationToken cancellationToken = default);

    Task ApplySnapshotsAsync(
        IReadOnlyCollection<ManagementProjectProgressSnapshotEventV1> snapshots,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default);
}
