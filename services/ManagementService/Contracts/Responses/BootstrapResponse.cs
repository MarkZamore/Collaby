namespace ManagementService.Contracts.Responses;

public class BootstrapResponse
{
    public string Version { get; set; } = "1";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Partial { get; set; }
    public List<string> Warnings { get; set; } = new();
    public BootstrapUserProfile? UserProfile { get; set; }
    public List<BootstrapWorkspace> Workspaces { get; set; } = new();
    public int PendingInvitesCount { get; set; }
}

public class BootstrapUserProfile
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}

public class BootstrapWorkspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public string Role { get; set; } = string.Empty;
    public List<BootstrapProject> Projects { get; set; } = new();
    public List<BootstrapMember> Members { get; set; } = new();
}

public class BootstrapProject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsArchived { get; set; }
    public List<ProjectStatusResponse> Statuses { get; set; } = new();
    public List<LabelResponse> Labels { get; set; } = new();
    public List<string>? BoardStickerIds { get; set; }
}

public class BootstrapMember
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
