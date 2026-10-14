using AnalyticsService.Application.Contracts;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Domain.Entities;
using AnalyticsService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Application.Services;

public sealed class ProjectAnalyticsOverviewService : IProjectAnalyticsOverviewService
{
    private static readonly string[] ProductivityEventNames =
    [
        "task-completed",
        "task-reopened",
        "subtask-completed",
        "comment-added"
    ];

    private readonly AnalyticsDbContext _dbContext;
    private readonly IUserLookupClient _userLookupClient;
    private readonly ILogger<ProjectAnalyticsOverviewService> _logger;

    public ProjectAnalyticsOverviewService(
        AnalyticsDbContext dbContext,
        IUserLookupClient userLookupClient,
        ILogger<ProjectAnalyticsOverviewService> logger)
    {
        _dbContext = dbContext;
        _userLookupClient = userLookupClient;
        _logger = logger;
    }

    public async Task<ProjectAnalyticsOverviewDto> BuildOverviewAsync(
        Guid projectId,
        ManagementProjectAnalyticsSnapshot snapshot,
        ProjectAnalyticsPeriod period,
        CancellationToken cancellationToken = default)
    {
        var summary = await _dbContext.ProjectAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.ProjectId == projectId, cancellationToken);

        var todayUtc = DateTime.UtcNow.Date;
        var bucketCount = (int)period;
        var rangeStartUtc = todayUtc.AddDays(-(bucketCount - 1));
        var rangeEndExclusiveUtc = todayUtc.AddDays(1);

        var events = await _dbContext.AnalyticsInboxEvents
            .AsNoTracking()
            .Where(entry =>
                entry.ProjectId == projectId &&
                entry.SourceService == "management-service" &&
                entry.OccurredAtUtc >= rangeStartUtc &&
                entry.OccurredAtUtc < rangeEndExclusiveUtc &&
                ProductivityEventNames.Contains(entry.EventType))
            .ToListAsync(cancellationToken);

        var productivity = BuildProductivityBuckets(rangeStartUtc, bucketCount, events);

        IReadOnlyDictionary<Guid, UserLookupResult> userLookup;
        try
        {
            userLookup = await _userLookupClient.GetUsersByIdsAsync(
                snapshot.Members.Select(member => member.UserId).Distinct().ToList(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Analytics member enrichment failed for project {ProjectId}", projectId);
            userLookup = new Dictionary<Guid, UserLookupResult>();
        }

        var memberDirectory = snapshot.Members
            .Select(member =>
            {
                userLookup.TryGetValue(member.UserId, out var user);
                return new ProjectMemberDirectoryEntryDto
                {
                    UserId = member.UserId,
                    Role = member.Role,
                    Username = user?.Username,
                    DisplayName = user?.DisplayName,
                    AvatarUrl = user?.AvatarUrl
                };
            })
            .ToList();

        return new ProjectAnalyticsOverviewDto
        {
            ProjectId = projectId,
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = MapSummary(summary),
            Productivity = productivity,
            StatusDistribution = snapshot.StatusDistribution,
            MemberDirectory = memberDirectory
        };
    }

    private static ProjectAnalyticsSummaryDto MapSummary(ProjectAnalyticsSummary? summary)
    {
        if (summary is null)
        {
            return new ProjectAnalyticsSummaryDto();
        }

        return new ProjectAnalyticsSummaryDto
        {
            TasksCompleted = summary.TasksCompleted,
            TasksReopened = summary.TasksReopened,
            SubtasksCompleted = summary.SubtasksCompleted,
            CommentsAdded = summary.CommentsAdded,
            TotalXpDelta = summary.TotalXpDelta,
            TotalCoinsDelta = summary.TotalCoinsDelta,
            UpdatedAtUtc = summary.UpdatedAtUtc
        };
    }

    private static IReadOnlyList<ProjectProductivityBucketDto> BuildProductivityBuckets(
        DateTime rangeStartUtc,
        int bucketCount,
        IReadOnlyCollection<AnalyticsInboxEvent> events)
    {
        var buckets = Enumerable.Range(0, bucketCount)
            .Select(offset => new ProjectProductivityBucketDto
            {
                BucketStartUtc = rangeStartUtc.AddDays(offset),
                TasksCompleted = 0,
                TasksReopened = 0,
                SubtasksCompleted = 0,
                CommentsAdded = 0
            })
            .ToDictionary(bucket => bucket.BucketStartUtc, bucket => bucket);

        foreach (var entry in events)
        {
            var bucketDate = entry.OccurredAtUtc.Date;
            if (!buckets.TryGetValue(bucketDate, out var bucket))
            {
                continue;
            }

            var normalizedEventName = NormalizeEventName(entry);
            switch (normalizedEventName)
            {
                case "task-completed":
                    bucket.TasksCompleted += 1;
                    break;
                case "task-reopened":
                    bucket.TasksReopened += 1;
                    break;
                case "subtask-completed":
                    bucket.SubtasksCompleted += 1;
                    break;
                case "comment-added":
                    bucket.CommentsAdded += 1;
                    break;
            }
        }

        return buckets.Values
            .OrderBy(bucket => bucket.BucketStartUtc)
            .ToList();
    }

    private static string NormalizeEventName(AnalyticsInboxEvent entry)
    {
        return string.IsNullOrWhiteSpace(entry.Metadata)
            ? entry.EventType.Trim().ToLowerInvariant()
            : entry.Metadata.Trim().ToLowerInvariant();
    }
}
