namespace IdentityService.Application.Settings;

public sealed class UserDeletionTargetDefinition
{
    public string ServiceName { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}
