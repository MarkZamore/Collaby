using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Interfaces;

public interface IUserDeletionOrchestrator
{
    Task<UserDeletionOperation> StartDeletionAsync(
        Guid targetUserId,
        Guid? requestedByUserId,
        string? requestedByEmail,
        CancellationToken cancellationToken = default);

    Task<UserDeletionOperation?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);

    Task<UserDeletionOperation> RetryAsync(Guid operationId, CancellationToken cancellationToken = default);

    Task ProcessPendingOperationsAsync(CancellationToken cancellationToken = default);

    Task ApplyDeletionResultAsync(
        EventMetadata metadata,
        AdminUserDeletionResultV1 result,
        CancellationToken cancellationToken = default);
}
