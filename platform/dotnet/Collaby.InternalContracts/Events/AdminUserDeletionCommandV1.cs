namespace Collaby.InternalContracts.Events;

public sealed record AdminUserDeletionCommandV1(
    Guid OperationId,
    Guid UserId,
    string TargetService,
    string RequestedBy);
