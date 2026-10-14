namespace ManagementService.Contracts.Responses.Admin;

public sealed class AdminWorkspaceListResponse
{
    public IReadOnlyCollection<AdminWorkspaceListItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class AdminWorkspaceListItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public int ProjectCount { get; set; }
    public int TaskCount { get; set; }
    public AdminUserSummaryResponse? Owner { get; set; }
}

public sealed class AdminWorkspaceDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public int ActiveMemberCount { get; set; }
    public int ProjectCount { get; set; }
    public int TaskCount { get; set; }
    public AdminUserSummaryResponse? Owner { get; set; }
    public IReadOnlyCollection<AdminWorkspaceMemberSummaryResponse> Members { get; set; } = [];
}

public sealed class AdminWorkspaceMemberSummaryResponse
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public AdminUserSummaryResponse? User { get; set; }
}

public sealed class AdminUserSummaryResponse
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
