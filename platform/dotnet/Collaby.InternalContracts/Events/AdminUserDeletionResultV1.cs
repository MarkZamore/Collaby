namespace Collaby.InternalContracts.Events;

public sealed record AdminUserDeletionResultV1(
    Guid OperationId,
    Guid UserId,
    string TargetService,
    string Status,
    int? DeletedRecords = null,
    string? FailureCode = null);
