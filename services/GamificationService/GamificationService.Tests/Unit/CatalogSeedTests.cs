using GamificationService.Domain.Constants;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class CatalogSeedTests
{
    [Fact]
    public async Task SeedCatalog_ShouldExposeUpdatedSeriesEconomyAndRankLabels()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var freezeDefinition = GamificationCatalog.ShopItems.Single(item => item.Id == GamificationCatalog.StreakFreezeItemId);
        var goldRankDefinition = GamificationCatalog.ShopItems.Single(item => item.Id == GamificationCatalog.GoldCrownBadgeId);
        var commentQuestDefinition = GamificationCatalog.Quests.Single(quest => quest.Key == "commentator");
        var tierThreeDefinition = GamificationCatalog.SkillTreeNodes.Single(node => node.Key == "projectVanguard");

        var freezeItem = await dbContext.ShopItems.SingleAsync(item => item.Id == GamificationCatalog.StreakFreezeItemId);
        var goldRank = await dbContext.ShopItems.SingleAsync(item => item.Id == GamificationCatalog.GoldCrownBadgeId);
        var commentQuest = await dbContext.Quests.SingleAsync(quest => quest.Id == commentQuestDefinition.Id);
        var tierThreeNode = await dbContext.SkillTreeNodes.SingleAsync(node =>
            node.ProjectId == GamificationCatalog.SkillTreeTemplateProjectId &&
            node.Tier == 3 &&
            node.Branch == "A");

        Assert.Equal(freezeDefinition.Price, freezeItem.Price);
        Assert.Equal(freezeDefinition.Name, freezeItem.Name);
        Assert.Equal(goldRankDefinition.Name, goldRank.Name);
        Assert.Equal(commentQuestDefinition.Description, commentQuest.Description);
        Assert.Equal(commentQuestDefinition.Requirement, commentQuest.Requirement);
        Assert.Equal(tierThreeDefinition.Description, tierThreeNode.Description);
    }

    [Fact]
    public async Task SeedCatalog_ShouldKeepCommentQuestDaily()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var commentQuestDefinition = GamificationCatalog.Quests.Single(quest => quest.Key == "commentator");
        var commentQuest = await dbContext.Quests.SingleAsync(quest => quest.Id == commentQuestDefinition.Id);
        Assert.Equal(QuestType.Daily, commentQuest.Type);
    }
}
