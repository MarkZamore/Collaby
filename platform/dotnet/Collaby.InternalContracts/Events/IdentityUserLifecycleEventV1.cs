namespace Collaby.InternalContracts.Events;

public sealed record IdentityUserLifecycleEventV1(
    Guid UserId,
    string LifecycleState,
    bool IsActive,
    bool IsSystemAdmin,
    string? Email = null);
