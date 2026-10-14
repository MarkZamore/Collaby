using GamificationService.Domain.Enums;

namespace GamificationService.Domain.Constants;

public sealed record SeededShopItemDefinition(
    Guid Id,
    string Key,
    string Name,
    string Category,
    int Price,
    string Description,
    bool IsPurchasable);

public sealed record SeededQuestDefinition(
    Guid Id,
    string Key,
    QuestType Type,
    string Title,
    string Description,
    int XpReward,
    int CoinReward,
    string Requirement);

public sealed record SeededSkillTreeNodeDefinition(
    Guid Id,
    string Key,
    int Tier,
    string Branch,
    string Title,
    string Description,
    string RewardJson);

public static class GamificationCatalog
{
    public static readonly DateTime SeedTimestampUtc = new(2026, 3, 13, 0, 0, 0, DateTimeKind.Utc);

    public static readonly Guid StreakFreezeItemId = Guid.Parse("a0000001-0000-0000-0000-000000000007");
    public static readonly Guid GoldCrownBadgeId = Guid.Parse("a0000001-0000-0000-0000-000000000008");
    public static readonly Guid SilverCrownBadgeId = Guid.Parse("a0000001-0000-0000-0000-000000000009");
    public static readonly Guid BronzeCrownBadgeId = Guid.Parse("a0000001-0000-0000-0000-000000000010");

    public static readonly Guid SkillTreeTemplateProjectId = Guid.Empty;

    public static readonly IReadOnlyList<SeededShopItemDefinition> ShopItems =
    [
        new(Guid.Parse("a0000001-0000-0000-0000-000000000001"), "goldFrame", "Gold Frame", "frames", 500, "A prestigious gold avatar frame", true),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000002"), "neonFrame", "Neon Frame", "frames", 1000, "A vibrant neon avatar frame", true),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000003"), "fireFrame", "Fire Frame", "frames", 2000, "An animated fire avatar frame", true),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000004"), "forestTheme", "Forest Theme", "themes", 800, "A calming forest color scheme", true),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000005"), "earlyAccessBadge", "Early Access Badge", "badges", 100, "Badge for early adopters", true),
        new(Guid.Parse("a0000001-0000-0000-0000-000000000006"), "proBadge", "PRO Badge", "badges", 5000, "The ultimate PRO badge", true),
        new(StreakFreezeItemId, "seriesFreeze", "Series Freeze", "consumables", 500, "Protects your series for 1 day", true),
        new(GoldCrownBadgeId, "goldCrown", "Gold Rank", "badges", 0, "Annual rank award for the global top performer", false),
        new(SilverCrownBadgeId, "silverCrown", "Silver Rank", "badges", 0, "Annual rank award for the global second place", false),
        new(BronzeCrownBadgeId, "bronzeCrown", "Bronze Rank", "badges", 0, "Annual rank award for the global third place", false),
    ];

    public static readonly IReadOnlyList<SeededQuestDefinition> Quests =
    [
        new(Guid.Parse("b0000001-0000-0000-0000-000000000001"), "taskStarter", QuestType.Daily, "Task Starter", "Complete 3 tasks", 15, 10, "{\"type\":\"complete_tasks\",\"count\":3}"),
        new(Guid.Parse("b0000001-0000-0000-0000-000000000002"), "commentator", QuestType.Daily, "Commentator", "Add 1 comment", 10, 5, "{\"type\":\"add_comments\",\"count\":1}"),
        new(Guid.Parse("b0000001-0000-0000-0000-000000000003"), "priorityHunter", QuestType.Daily, "Priority Hunter", "Complete 1 high-priority task", 20, 15, "{\"type\":\"complete_high_priority\",\"count\":1}"),
        new(Guid.Parse("b0000001-0000-0000-0000-000000000004"), "workhorse", QuestType.Weekly, "Workhorse", "Complete 15 tasks this week", 50, 30, "{\"type\":\"complete_tasks\",\"count\":15}"),
        new(Guid.Parse("b0000001-0000-0000-0000-000000000005"), "streakMaster", QuestType.Weekly, "Streak Master", "Maintain a 7-day streak", 30, 20, "{\"type\":\"maintain_streak\",\"count\":7}"),
    ];

    public static readonly IReadOnlyList<SeededSkillTreeNodeDefinition> SkillTreeNodes =
    [
        new(Guid.Parse("c0000001-0000-0000-0000-000000000001"), "executionFocus", 1, "A", "Execution Focus", "Sharpen delivery speed for your first specialization.", "{\"title\":\"Executor\",\"xpMultiplier\":1.05}"),
        new(Guid.Parse("c0000001-0000-0000-0000-000000000002"), "teamSupport", 1, "B", "Team Support", "Boost collaborative impact and unlock support-oriented rewards.", "{\"title\":\"Supporter\",\"coinBonus\":10}"),
        new(Guid.Parse("c0000001-0000-0000-0000-000000000003"), "momentumBuilder", 2, "A", "Momentum Builder", "Turn consistent progress into stronger XP gains.", "{\"title\":\"Momentum Builder\",\"xpMultiplier\":1.1}"),
        new(Guid.Parse("c0000001-0000-0000-0000-000000000004"), "mentorAura", 2, "B", "Mentor Aura", "Earn cosmetic recognition for guiding the project forward.", "{\"badge\":\"mentor-aura\"}"),
        new(Guid.Parse("c0000001-0000-0000-0000-000000000005"), "projectVanguard", 3, "A", "Project Vanguard", "Final reward for users leading high-output delivery.", "{\"title\":\"Project Vanguard\",\"xpMultiplier\":1.15}"),
        new(Guid.Parse("c0000001-0000-0000-0000-000000000006"), "cultureCatalyst", 3, "B", "Culture Catalyst", "Final reward centered on team energy and shared gains.", "{\"badge\":\"culture-catalyst\",\"coinBonus\":20}"),
    ];
}
