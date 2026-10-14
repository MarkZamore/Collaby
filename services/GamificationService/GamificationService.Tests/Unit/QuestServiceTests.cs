using System.Globalization;
using GamificationService.Application.Interfaces;
using GamificationService.Application.Services;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class QuestServiceTests
{
    [Fact]
    public async Task GetActiveQuestsAsync_ShouldRecycleQuestForNewWindow()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        var userId = Guid.NewGuid();
        var questId = Guid.NewGuid();
        var yesterdayKey = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        dbContext.Quests.Add(new Quest
        {
            Id = questId,
            Type = QuestType.Daily,
            Title = "Daily Comment",
            Description = "Add 1 comment",
            Requirement = "{\"type\":\"add_comments\",\"count\":1}",
            XpReward = 10,
            CoinReward = 5,
            IsActive = true
        });

        dbContext.UserQuests.Add(new UserQuest
        {
            UserId = userId,
            QuestId = questId,
            WindowKey = yesterdayKey,
            Progress = 1,
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            ClaimedAt = DateTime.UtcNow.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();

        var service = new QuestService(
            dbContext,
            new StubXpService(),
            new StubStreakService(),
            new StubLeaderboardService(),
            NullLogger<QuestService>.Instance);

        _ = await service.GetActiveQuestsAsync(userId);

        var recycled = await dbContext.UserQuests.SingleAsync(userQuest => userQuest.UserId == userId && userQuest.QuestId == questId);
        Assert.Equal(todayKey, recycled.WindowKey);
        Assert.Equal(0, recycled.Progress);
        Assert.False(recycled.IsCompleted);
        Assert.Null(recycled.CompletedAt);
        Assert.Null(recycled.ClaimedAt);
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldCompleteSingleCommentQuestOnFirstComment()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        var userId = Guid.NewGuid();
        var questId = Guid.NewGuid();
        var todayKey = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        dbContext.Quests.Add(new Quest
        {
            Id = questId,
            Type = QuestType.Daily,
            Title = "Daily Comment",
            Description = "Add 1 comment",
            Requirement = "{\"type\":\"add_comments\",\"count\":1}",
            XpReward = 10,
            CoinReward = 5,
            IsActive = true
        });

        dbContext.UserQuests.Add(new UserQuest
        {
            UserId = userId,
            QuestId = questId,
            WindowKey = todayKey
        });

        await dbContext.SaveChangesAsync();

        var service = new QuestService(
            dbContext,
            new StubXpService(),
            new StubStreakService(),
            new StubLeaderboardService(),
            NullLogger<QuestService>.Instance);

        await service.UpdateProgressAsync(userId, "comment-added");

        var quest = await dbContext.UserQuests.SingleAsync(userQuest => userQuest.UserId == userId && userQuest.QuestId == questId);
        Assert.Equal(1, quest.Progress);
        Assert.True(quest.IsCompleted);
        Assert.NotNull(quest.CompletedAt);
    }

    private sealed class StubXpService : IXpService
    {
        public Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, XpReason reason, Guid? taskId = null, string? priority = null)
            => Task.FromResult(new RewardEventResponse());

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

    private sealed class StubStreakService : IStreakService
    {
        public Task RecordActivityAsync(Guid userId) => Task.CompletedTask;

        public Task<StreakResponse> GetStreakAsync(Guid userId)
            => Task.FromResult(new StreakResponse
            {
                StreakDays = 0,
                Multiplier = 1.0
            });

        public Task<bool> UseFreezeAsync(Guid userId) => Task.FromResult(false);

        public double GetStreakMultiplier(int streakDays) => 1.0;
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
}
