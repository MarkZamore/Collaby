namespace GamificationService.Contracts.Responses;

public class UserXpResponse
{
    public Guid UserId { get; set; }
    public long TotalXp { get; set; }
    public int Level { get; set; }
    public long XpToNextLevel { get; set; }
    public long Coins { get; set; }
    public int StreakDays { get; set; }
    public int FreezeCount { get; set; }
}

public class UserProjectXpResponse
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public long Xp { get; set; }
    public int Level { get; set; }
    public long XpToNextLevel { get; set; }
    public int TasksCompleted { get; set; }
}

public class XpHistoryItemResponse
{
    public Guid Id { get; set; }
    public long Amount { get; set; }
    public long CoinsAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeaderboardEntryResponse
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
    public long Xp { get; set; }
    public int Level { get; set; }
}

public class QuestResponse
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int XpReward { get; set; }
    public int CoinReward { get; set; }
    public int Progress { get; set; }
    public int RequiredCount { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsClaimed { get; set; }
}

public class StreakResponse
{
    public int StreakDays { get; set; }
    public int FreezeCount { get; set; }
    public double Multiplier { get; set; }
    public DateTime? LastActiveDate { get; set; }
    public bool IsActiveToday { get; set; }
}

public class ShopItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsPurchasable { get; set; }
    public bool IsOwned { get; set; }
    public bool IsEquipped { get; set; }
}

public class InventoryItemResponse
{
    public Guid ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsEquipped { get; set; }
    public DateTime PurchasedAt { get; set; }
}

public class ProjectProgressResponse
{
    public Guid ProjectId { get; set; }
    public double ProgressPercent { get; set; }
    public double TotalWeight { get; set; }
    public double CompletedWeight { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int TotalSubtasks { get; set; }
    public int CompletedSubtasks { get; set; }
}

public class SkillTreeNodeResponse
{
    public Guid Id { get; set; }
    public int Tier { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RewardJson { get; set; } = "{}";
    public bool IsChosen { get; set; }
    public bool IsAvailable { get; set; }
}

public class SkillTreeConfigNodeResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public int Tier { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RewardJson { get; set; } = "{}";
    public bool IsActive { get; set; }
}

public class RewardEventResponse
{
    public long XpAwarded { get; set; }
    public long CoinsAwarded { get; set; }
    public int NewLevel { get; set; }
    public bool LeveledUp { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool RewardApplied { get; set; } = true;
    public bool CountedTowardsQuestProgress { get; set; } = true;
}
