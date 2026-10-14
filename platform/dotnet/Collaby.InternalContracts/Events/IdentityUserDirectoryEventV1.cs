namespace Collaby.InternalContracts.Events;

public sealed record IdentityUserDirectoryEventV1(
    Guid UserId,
    string Username,
    string Email,
    bool IsActive,
    bool IsSystemAdmin,
    string LifecycleState);
