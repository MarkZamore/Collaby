namespace Collaby.InternalContracts.Events;

public sealed record ManagementWorkspaceEventV1(
    Guid WorkspaceId,
    Guid OwnerUserId,
    string EventName,
    int MemberCount,
    bool IsArchived);
