using AnalyticsService.Domain.Entities;
using AnalyticsService.Infrastructure.Data;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Application.Services;

public sealed class AnalyticsProjectionService
{
    private readonly AnalyticsDbContext _dbContext;

    public AnalyticsProjectionService(AnalyticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AnalyticsIngestionResult> IngestManagementTaskAsync(
        EventMetadata metadata,
        ManagementTaskEventV1 payload,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AnalyticsInboxEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(evt => evt.EventId == metadata.EventId, cancellationToken);

        if (existing is not null)
        {
            return AnalyticsIngestionResult.Duplicate(existing.IsProjectionSupported);
        }

        var isSupported = await ApplyManagementDeltaAsync(payload, cancellationToken);
        _dbContext.AnalyticsInboxEvents.Add(new AnalyticsInboxEvent
        {
            EventId = metadata.EventId,
            SourceService = metadata.SourceService,
            EventType = metadata.EventType,
            OccurredAtUtc = metadata.OccurredAtUtc,
            UserId = payload.ActorUserId,
            ProjectId = payload.ProjectId,
            TaskId = payload.TaskId,
            Priority = payload.Priority,
            Metadata = payload.EventName,
            IsProjectionSupported = isSupported,
            ReceivedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AnalyticsIngestionResult.Processed(isSupported);
    }

    public async Task<AnalyticsIngestionResult> IngestGamificationProgressAsync(
        EventMetadata metadata,
        GamificationProgressEventV1 payload,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AnalyticsInboxEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(evt => evt.EventId == metadata.EventId, cancellationToken);

        if (existing is not null)
        {
            return AnalyticsIngestionResult.Duplicate(existing.IsProjectionSupported);
        }

        var projectId = await ResolveProjectIdFromCausationAsync(metadata, cancellationToken);
        var isSupported = await ApplyRewardDeltaAsync(payload, projectId, cancellationToken);
        _dbContext.AnalyticsInboxEvents.Add(new AnalyticsInboxEvent
        {
            EventId = metadata.EventId,
            SourceService = metadata.SourceService,
            EventType = metadata.EventType,
            OccurredAtUtc = metadata.OccurredAtUtc,
            UserId = payload.UserId,
            ProjectId = projectId,
            Metadata = payload.RewardCode,
            XpDelta = payload.XpDelta,
            CoinsDelta = payload.CoinsDelta,
            LevelAfterEvent = payload.LevelAfterEvent,
            IsProjectionSupported = isSupported,
            ReceivedAtUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AnalyticsIngestionResult.Processed(isSupported);
    }

    private async Task<bool> ApplyManagementDeltaAsync(
        ManagementTaskEventV1 payload,
        CancellationToken cancellationToken)
    {
        var userSummary = await GetOrCreateUserSummaryAsync(payload.ActorUserId, cancellationToken);
        userSummary.UpdatedAtUtc = DateTime.UtcNow;

        var projectSummary = await GetOrCreateProjectSummaryAsync(payload.ProjectId, cancellationToken);
        projectSummary.UpdatedAtUtc = DateTime.UtcNow;

        switch (payload.EventName)
        {
            case "task-completed":
                userSummary.TasksCompleted += 1;
                projectSummary.TasksCompleted += 1;
                return true;
            case "task-reopened":
                userSummary.TasksReopened += 1;
                projectSummary.TasksReopened += 1;
                return true;
            case "subtask-completed":
                userSummary.SubtasksCompleted += 1;
                projectSummary.SubtasksCompleted += 1;
                return true;
            case "comment-added":
                userSummary.CommentsAdded += 1;
                projectSummary.CommentsAdded += 1;
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> ApplyRewardDeltaAsync(
        GamificationProgressEventV1 payload,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var userSummary = await GetOrCreateUserSummaryAsync(payload.UserId, cancellationToken);
        userSummary.TotalXpDelta += payload.XpDelta;
        userSummary.TotalCoinsDelta += payload.CoinsDelta;
        userSummary.CurrentLevel = payload.LevelAfterEvent;
        userSummary.UpdatedAtUtc = DateTime.UtcNow;

        if (projectId is { } resolvedProjectId)
        {
            var projectSummary = await GetOrCreateProjectSummaryAsync(resolvedProjectId, cancellationToken);
            projectSummary.TotalXpDelta += payload.XpDelta;
            projectSummary.TotalCoinsDelta += payload.CoinsDelta;
            projectSummary.UpdatedAtUtc = DateTime.UtcNow;
        }

        return true;
    }

    private async Task<Guid?> ResolveProjectIdFromCausationAsync(EventMetadata metadata, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(metadata.CausationId, out var causationEventId))
        {
            return null;
        }

        return await _dbContext.AnalyticsInboxEvents
            .AsNoTracking()
            .Where(evt => evt.EventId == causationEventId)
            .Select(evt => evt.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<UserAnalyticsSummary> GetOrCreateUserSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var summary = await _dbContext.UserAnalyticsSummaries.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (summary is not null)
        {
            return summary;
        }

        summary = new UserAnalyticsSummary
        {
            UserId = userId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.UserAnalyticsSummaries.Add(summary);
        return summary;
    }

    private async Task<ProjectAnalyticsSummary> GetOrCreateProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var summary = await _dbContext.ProjectAnalyticsSummaries.FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);
        if (summary is not null)
        {
            return summary;
        }

        summary = new ProjectAnalyticsSummary
        {
            ProjectId = projectId,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.ProjectAnalyticsSummaries.Add(summary);
        return summary;
    }
}

public sealed record AnalyticsIngestionResult(bool IsDuplicate, bool IsProjectionSupported)
{
    public static AnalyticsIngestionResult Duplicate(bool isProjectionSupported) => new(true, isProjectionSupported);
    public static AnalyticsIngestionResult Processed(bool isProjectionSupported) => new(false, isProjectionSupported);
}
