namespace Collaby.InternalContracts.Events;

public sealed record UserProfileSnapshotEventV1(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl = null,
    string? Locale = null);
