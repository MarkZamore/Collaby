using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using ManagementService.TestCommon;
using GamificationService.Application.Interfaces;
using GamificationService.Application.Services;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Hubs;
using GamificationService.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class InternalControllerEventPipelineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ManagementTaskEventProcessor_ShouldDeduplicateEvent_AndEnqueueAnalyticsOutboxOnce()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var processor = new ManagementTaskEventProcessor(
            dbContext,
            new StubXpService(),
            new StubQuestService(),
            new StubLeaderboardService(),
            new StubEventJournal(),
            new AnalyticsRewardOutbox(dbContext),
            new NoOpHubContext<GamificationHub>(),
            NullLogger<ManagementTaskEventProcessor>.Instance);

        var sourceEvent = new ConsumedEvent<ManagementTaskEventV1>(
            EventTopicNames.ManagementTaskEventsV1,
            Guid.NewGuid().ToString("D"),
            new EventMetadata(
                Guid.NewGuid(),
                "task-completed",
                "v1",
                "management-service",
                "corr-management-request",
                "corr-management-request",
                DateTime.UtcNow),
            new ManagementTaskEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "task-completed",
                "high"));

        await processor.ProcessAsync(sourceEvent, CancellationToken.None);

        var processedEvent = await dbContext.ProcessedIntegrationEvents.SingleAsync();
        var outboxMessage = await dbContext.IntegrationOutboxMessages.SingleAsync();
        var analyticsEnvelope = JsonSerializer.Deserialize<PersistedEventEnvelope<GamificationProgressEventV1>>(outboxMessage.PayloadJson, JsonOptions);

        Assert.Equal(sourceEvent.Metadata.EventId, processedEvent.Id);
        Assert.NotNull(analyticsEnvelope);
        Assert.Equal("reward-applied", analyticsEnvelope!.EventType);
        Assert.Equal("corr-management-request", analyticsEnvelope.CorrelationId);
        Assert.Equal(sourceEvent.Metadata.EventId.ToString("N"), analyticsEnvelope.CausationId);
        Assert.Equal(25, analyticsEnvelope.Payload.XpDelta);
        Assert.Equal(10, analyticsEnvelope.Payload.CoinsDelta);
        Assert.Equal(3, analyticsEnvelope.Payload.LevelAfterEvent);
        Assert.Equal("TaskCompleted", analyticsEnvelope.Payload.RewardCode);
        Assert.Equal(sourceEvent.Payload.ActorUserId, analyticsEnvelope.Payload.UserId);

        await processor.ProcessAsync(sourceEvent, CancellationToken.None);

        Assert.Equal(1, await dbContext.ProcessedIntegrationEvents.CountAsync());
        Assert.Equal(1, await dbContext.IntegrationOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ManagementTaskEventProcessor_ShouldSkipQuestAndOutbox_ForNoOpReward()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var questService = new RecordingQuestService();
        var processor = new ManagementTaskEventProcessor(
            dbContext,
            new NoOpRewardXpService(),
            questService,
            new StubLeaderboardService(),
            new StubEventJournal(),
            new AnalyticsRewardOutbox(dbContext),
            new NoOpHubContext<GamificationHub>(),
            NullLogger<ManagementTaskEventProcessor>.Instance);

        var sourceEvent = new ConsumedEvent<ManagementTaskEventV1>(
            EventTopicNames.ManagementTaskEventsV1,
            Guid.NewGuid().ToString("D"),
            new EventMetadata(
                Guid.NewGuid(),
                "comment-added",
                "v1",
                "management-service",
                "corr-comment",
                "corr-comment",
                DateTime.UtcNow),
            new ManagementTaskEventV1(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "comment-added",
                "medium"));

        await processor.ProcessAsync(sourceEvent, CancellationToken.None);

        Assert.False(questService.UpdateCalled);
        Assert.Equal(1, await dbContext.ProcessedIntegrationEvents.CountAsync());
        Assert.Equal(0, await dbContext.IntegrationOutboxMessages.CountAsync());
    }

    private sealed class StubXpService : IXpService
    {
        public Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, XpReason reason, Guid? taskId = null, string? priority = null)
            => Task.FromResult(new RewardEventResponse
            {
                XpAwarded = 25,
                CoinsAwarded = 10,
                NewLevel = 3,
                LeveledUp = true,
                Reason = reason.ToString()
            });

        public Task EnsureUserExistsAsync(Guid userId) => Task.CompletedTask;

        public Task<UserXpResponse> GetUserGlobalXpAsync(Guid userId)
            => throw new NotSupportedException();

        public Task<IEnumerable<XpHistoryItemResponse>> GetXpHistoryAsync(Guid userId, int limit = 50, int offset = 0)
            => throw new NotSupportedException();

        public Task<UserProjectXpResponse> GetUserProjectXpAsync(Guid userId, Guid projectId)
            => throw new NotSupportedException();

        public Task<RewardEventResponse> RollbackXpAsync(Guid userId, Guid? projectId, Guid taskId)
            => throw new NotSupportedException();
    }

    private sealed class NoOpRewardXpService : IXpService
    {
        public Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, XpReason reason, Guid? taskId = null, string? priority = null)
            => Task.FromResult(new RewardEventResponse
            {
                XpAwarded = 0,
                CoinsAwarded = 0,
                NewLevel = 1,
                LeveledUp = false,
                Reason = reason.ToString(),
                RewardApplied = false,
                CountedTowardsQuestProgress = false
            });

        public Task EnsureUserExistsAsync(Guid userId) => Task.CompletedTask;

        public Task<UserXpResponse> GetUserGlobalXpAsync(Guid userId)
            => throw new NotSupportedException();

        public Task<IEnumerable<XpHistoryItemResponse>> GetXpHistoryAsync(Guid userId, int limit = 50, int offset = 0)
            => throw new NotSupportedException();

        public Task<UserProjectXpResponse> GetUserProjectXpAsync(Guid userId, Guid projectId)
            => throw new NotSupportedException();

        public Task<RewardEventResponse> RollbackXpAsync(Guid userId, Guid? projectId, Guid taskId)
            => throw new NotSupportedException();
    }

    private sealed class StubQuestService : IQuestService
    {
        public Task AssignDailyQuestsAsync(Guid userId) => Task.CompletedTask;

        public Task<RewardEventResponse?> ClaimQuestAsync(Guid userId, Guid questId)
            => throw new NotSupportedException();

        public Task<IEnumerable<QuestResponse>> GetActiveQuestsAsync(Guid userId)
            => throw new NotSupportedException();

        public Task UpdateProgressAsync(Guid userId, string eventType, string? priority = null)
            => Task.CompletedTask;
    }

    private sealed class RecordingQuestService : IQuestService
    {
        public bool UpdateCalled { get; private set; }

        public Task AssignDailyQuestsAsync(Guid userId) => Task.CompletedTask;

        public Task<RewardEventResponse?> ClaimQuestAsync(Guid userId, Guid questId)
            => throw new NotSupportedException();

        public Task<IEnumerable<QuestResponse>> GetActiveQuestsAsync(Guid userId)
            => throw new NotSupportedException();

        public Task UpdateProgressAsync(Guid userId, string eventType, string? priority = null)
        {
            UpdateCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubLeaderboardService : ILeaderboardService
    {
        public Task CreateAnnualSnapshotAsync(int year) => Task.CompletedTask;

        public Task<IEnumerable<LeaderboardEntryResponse>> GetGlobalTopAsync(int count = 10)
            => throw new NotSupportedException();

        public Task<IEnumerable<LeaderboardEntryResponse>> GetProjectTopAsync(Guid projectId, int count = 10)
            => throw new NotSupportedException();

        public Task RefreshGlobalLeaderboardAsync() => Task.CompletedTask;

        public Task RefreshProjectLeaderboardAsync(Guid projectId) => Task.CompletedTask;
    }

    private sealed class StubEventJournal : IGamificationEventJournal
    {
        public Task AppendAsync(
            ConsumedEvent<ManagementTaskEventV1> request,
            XpReason? reason,
            RewardEventResponse? reward,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
