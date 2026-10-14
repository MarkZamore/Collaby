namespace UserService.Domain.Entities;

public sealed class IdentityDirectoryEntry
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemAdmin { get; set; }
    public string LifecycleState { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}
