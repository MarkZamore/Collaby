namespace Collaby.InternalContracts.Events;

public sealed record ManagementTaskEventV1(
    Guid WorkspaceId,
    Guid ProjectId,
    Guid TaskId,
    Guid ActorUserId,
    string EventName,
    string? Priority = null);
