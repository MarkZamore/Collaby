namespace AnalyticsService.Application.Contracts;

public enum ProjectAnalyticsPeriod
{
    Week = 7,
    Month = 30
}

public sealed class ProjectAnalyticsOverviewDto
{
    public Guid ProjectId { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    public ProjectAnalyticsSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ProjectProductivityBucketDto> Productivity { get; init; } = [];
    public IReadOnlyList<ProjectStatusDistributionDto> StatusDistribution { get; init; } = [];
    public IReadOnlyList<ProjectMemberDirectoryEntryDto> MemberDirectory { get; init; } = [];
}

public sealed class ProjectAnalyticsSummaryDto
{
    public int TasksCompleted { get; init; }
    public int TasksReopened { get; init; }
    public int SubtasksCompleted { get; init; }
    public int CommentsAdded { get; init; }
    public long TotalXpDelta { get; init; }
    public long TotalCoinsDelta { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed class ProjectProductivityBucketDto
{
    public DateTime BucketStartUtc { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksReopened { get; set; }
    public int SubtasksCompleted { get; set; }
    public int CommentsAdded { get; set; }
}

public sealed class ProjectStatusDistributionDto
{
    public Guid StatusId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public int Order { get; init; }
    public bool IsFinal { get; init; }
    public int TaskCount { get; init; }
}

public sealed class ProjectMemberDirectoryEntryDto
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}

public sealed class ManagementProjectAnalyticsSnapshot
{
    public Guid ProjectId { get; init; }
    public Guid WorkspaceId { get; init; }
    public bool HasAccess { get; init; }
    public IReadOnlyList<ProjectStatusDistributionDto> StatusDistribution { get; init; } = [];
    public IReadOnlyList<ManagementProjectMemberSnapshotDto> Members { get; init; } = [];
}

public sealed class ManagementProjectMemberSnapshotDto
{
    public Guid UserId { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed class UserLookupResult
{
    public Guid UserId { get; init; }
    public string? Username { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
}
