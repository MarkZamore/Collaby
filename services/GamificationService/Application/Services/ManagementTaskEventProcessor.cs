using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Hubs;
using GamificationService.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Application.Services;

public sealed class ManagementTaskEventProcessor
{
    private readonly GamificationDbContext _dbContext;
    private readonly IXpService _xpService;
    private readonly IQuestService _questService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly IGamificationEventJournal _eventJournal;
    private readonly IAnalyticsRewardOutbox _analyticsRewardOutbox;
    private readonly IHubContext<GamificationHub> _hubContext;
    private readonly ILogger<ManagementTaskEventProcessor> _logger;

    public ManagementTaskEventProcessor(
        GamificationDbContext dbContext,
        IXpService xpService,
        IQuestService questService,
        ILeaderboardService leaderboardService,
        IGamificationEventJournal eventJournal,
        IAnalyticsRewardOutbox analyticsRewardOutbox,
        IHubContext<GamificationHub> hubContext,
        ILogger<ManagementTaskEventProcessor> logger)
    {
        _dbContext = dbContext;
        _xpService = xpService;
        _questService = questService;
        _leaderboardService = leaderboardService;
        _eventJournal = eventJournal;
        _analyticsRewardOutbox = analyticsRewardOutbox;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task ProcessAsync(
        ConsumedEvent<ManagementTaskEventV1> message,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ProcessedIntegrationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(evt => evt.Id == message.Metadata.EventId, cancellationToken);

        if (existing is not null)
        {
            _logger.LogDebug(
                "Skipped duplicate management task event {EventId} for user {UserId}.",
                message.Metadata.EventId,
                message.Payload.ActorUserId);
            return;
        }

        var reason = message.Payload.EventName switch
        {
            "task-completed" => XpReason.TaskCompleted,
            "subtask-completed" => XpReason.SubtaskCompleted,
            "comment-added" => XpReason.CommentAdded,
            "task-reopened" => XpReason.TaskReopened,
            _ => (XpReason?)null
        };

        if (reason is null)
        {
            throw new InvalidOperationException($"Unknown management task event: {message.Payload.EventName}");
        }

        RewardEventResponse? reward;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (reason == XpReason.TaskReopened)
            {
                reward = await _xpService.RollbackXpAsync(
                    message.Payload.ActorUserId,
                    message.Payload.ProjectId,
                    message.Payload.TaskId);

                await _leaderboardService.RefreshGlobalLeaderboardAsync();
                await _leaderboardService.RefreshProjectLeaderboardAsync(message.Payload.ProjectId);
            }
            else
            {
                reward = await _xpService.AwardXpAsync(
                    message.Payload.ActorUserId,
                    message.Payload.ProjectId,
                    reason.Value,
                    message.Payload.TaskId,
                    message.Payload.Priority);

                if (reward.CountedTowardsQuestProgress)
                {
                    await _questService.UpdateProgressAsync(
                        message.Payload.ActorUserId,
                        message.Payload.EventName,
                        message.Payload.Priority);
                }

                if (reward.RewardApplied)
                {
                    await _leaderboardService.RefreshGlobalLeaderboardAsync();
                    await _leaderboardService.RefreshProjectLeaderboardAsync(message.Payload.ProjectId);
                }
            }

            _dbContext.ProcessedIntegrationEvents.Add(new ProcessedIntegrationEvent
            {
                Id = message.Metadata.EventId,
                SourceService = message.Metadata.SourceService,
                EventType = message.Payload.EventName,
                ResponseJson = JsonSerializer.Serialize(reward),
                ProcessedAtUtc = DateTime.UtcNow
            });

            if (reward.RewardApplied)
            {
                await _analyticsRewardOutbox.EnqueueAsync(message, reward, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (reward.RewardApplied)
        {
            await TryAppendJournalAsync(message, reason, reward, cancellationToken);
            await NotifyUser(message.Payload.ActorUserId, reward);
        }
    }

    private async Task TryAppendJournalAsync(
        ConsumedEvent<ManagementTaskEventV1> message,
        XpReason? reason,
        RewardEventResponse reward,
        CancellationToken cancellationToken)
    {
        try
        {
            await _eventJournal.AppendAsync(message, reason, reward, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Gamification event {EventId} was processed but journal persistence degraded.",
                message.Metadata.EventId);
        }
    }

    private async Task NotifyUser(Guid userId, RewardEventResponse payload)
    {
        try
        {
            await _hubContext.Clients.Group($"user:{userId}").SendAsync("RewardReceived", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification to user {UserId}", userId);
        }
    }
}
