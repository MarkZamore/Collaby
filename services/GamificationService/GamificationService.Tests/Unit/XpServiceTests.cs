using GamificationService.Application.Interfaces;
using GamificationService.Application.Services;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class XpServiceTests
{
    [Fact]
    public async Task AwardXpAsync_ShouldPersistTransactionAndProjectProgress()
    {
        await using var dbContext = CreateDbContext();
        var streakService = new StubStreakService();
        var service = new XpService(dbContext, streakService, NullLogger<XpService>.Instance);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        RewardEventResponse result = await service.AwardXpAsync(userId, projectId, XpReason.TaskCompleted, taskId, "high");

        Assert.Equal(15, result.XpAwarded);
        Assert.Equal(8, result.CoinsAwarded);
        Assert.True(streakService.RecordActivityCalled);

        var global = await dbContext.UserGlobalXp.SingleAsync(x => x.UserId == userId);
        Assert.Equal(15, global.TotalXp);
        Assert.Equal(8, global.Coins);

        var project = await dbContext.UserProjectXp.SingleAsync(x => x.UserId == userId && x.ProjectId == projectId);
        Assert.Equal(15, project.Xp);
        Assert.Equal(1, project.TasksCompleted);
    }

    [Theory]
    [InlineData(0, 1, 200)]
    [InlineData(199, 1, 1)]
    [InlineData(200, 2, 100)]
    [InlineData(299, 2, 1)]
    [InlineData(300, 3, 100)]
    public void LevelThresholds_ShouldFollowGamificationRules(long totalXp, int expectedLevel, long expectedXpToNextLevel)
    {
        var level = XpService.CalculateLevel(totalXp);
        var xpToNextLevel = XpService.XpForLevel(level + 1) - totalXp;

        Assert.Equal(expectedLevel, level);
        Assert.Equal(expectedXpToNextLevel, xpToNextLevel);
    }

    [Fact]
    public async Task AwardXpAsync_ShouldDeduplicateCommentRewards_PerTaskAndUtcDay()
    {
        await using var dbContext = CreateDbContext();
        var streakService = new StubStreakService();
        var service = new XpService(dbContext, streakService, NullLogger<XpService>.Instance);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        RewardEventResponse first = await service.AwardXpAsync(userId, projectId, XpReason.CommentAdded, taskId);
        RewardEventResponse second = await service.AwardXpAsync(userId, projectId, XpReason.CommentAdded, taskId);

        Assert.Equal(2, first.XpAwarded);
        Assert.True(first.RewardApplied);
        Assert.True(first.CountedTowardsQuestProgress);

        Assert.Equal(0, second.XpAwarded);
        Assert.False(second.RewardApplied);
        Assert.False(second.CountedTowardsQuestProgress);

        var global = await dbContext.UserGlobalXp.SingleAsync(x => x.UserId == userId);
        Assert.Equal(2, global.TotalXp);

        var transactions = await dbContext.XpTransactions
            .Where(x => x.UserId == userId && x.TaskId == taskId && x.Reason == XpReason.CommentAdded)
            .ToListAsync();
        Assert.Single(transactions);
    }

    private static GamificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GamificationDbContext(options);
    }

    private sealed class StubStreakService : IStreakService
    {
        public bool RecordActivityCalled { get; private set; }

        public double GetStreakMultiplier(int streakDays) => 1.0;

        public Task<StreakResponse> GetStreakAsync(Guid userId)
            => Task.FromResult(new StreakResponse
            {
                StreakDays = 0,
                Multiplier = 1.0
            });

        public Task RecordActivityAsync(Guid userId)
        {
            RecordActivityCalled = true;
            return Task.CompletedTask;
        }

        public Task<bool> UseFreezeAsync(Guid userId) => Task.FromResult(false);
    }
}
