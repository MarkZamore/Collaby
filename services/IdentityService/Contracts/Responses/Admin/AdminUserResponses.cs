namespace IdentityService.Contracts.Responses.Admin;

public sealed class AdminUserListResponse
{
    public IReadOnlyCollection<AdminUserListItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class AdminUserListItemResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool IsSystemAdmin { get; set; }
    public bool IsProtectedAccount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public Guid? DeletionOperationId { get; set; }
    public string? DeletionStatus { get; set; }
}

public sealed class AdminUserDetailResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public bool EmailVerified { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool IsSystemAdmin { get; set; }
    public bool IsProtectedAccount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public UserDeletionOperationResponse? DeletionOperation { get; set; }
}

public sealed class UserDeletionOperationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureMessage { get; set; }
    public int RetryCount { get; set; }
    public bool CanRetry { get; set; }
    public IReadOnlyCollection<UserDeletionOperationStepResponse> Steps { get; set; } = [];
}

public sealed class UserDeletionOperationStepResponse
{
    public string ServiceName { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class SystemAdminRosterResponse
{
    public IReadOnlyCollection<SystemAdminRosterEntryResponse> Entries { get; set; } = [];
}

public sealed class SystemAdminRosterEntryResponse
{
    public string Email { get; set; } = string.Empty;
    public bool IsRegistered { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public bool? IsActive { get; set; }
}
