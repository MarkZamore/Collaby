namespace GamificationService.Domain.Enums;

public enum XpReason
{
    TaskCompleted = 0,
    SubtaskCompleted = 1,
    CommentAdded = 2,
    QuestCompleted = 3,
    StreakBonus = 4,
    LevelUpBonus = 5,
    ProjectCompleted = 6,
    SkillTreeReward = 7,
    AnnualAward = 8,
    AdminAdjustment = 9,
    TaskReopened = 10 // Отрицательная транзакция, откатывающая награду за повторно открытую задачу.
}
