using System.Globalization;
using System.Text.Json;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Application.Services;

/// <summary>
/// Отвечает за жизненный цикл ежедневных и недельных квестов.
/// Сервис назначает окна активности, сопоставляет игровые события требованиям,
/// накапливает прогресс и переводит выполненный квест в фазу выдачи награды.
/// </summary>
public class QuestService : IQuestService
{
    private readonly GamificationDbContext _db;
    private readonly IXpService _xpService;
    private readonly IStreakService _streakService;
    private readonly ILeaderboardService _leaderboardService;
    private readonly ILogger<QuestService> _logger;

    public QuestService(
        GamificationDbContext db,
        IXpService xpService,
        IStreakService streakService,
        ILeaderboardService leaderboardService,
        ILogger<QuestService> logger)
    {
        _db = db;
        _xpService = xpService;
        _streakService = streakService;
        _leaderboardService = leaderboardService;
        _logger = logger;
    }

    public async Task<IEnumerable<QuestResponse>> GetActiveQuestsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        // Перед выдачей списка квестов сервис сначала гарантирует наличие нужных user-quest записей,
        // а затем синхронизирует window-based квесты, например недельный streak-квест.
        await EnsureActiveQuestsAsync(userId, now);
        await RefreshWindowQuestStatesAsync(userId, now);

        var (dailyKey, weeklyKey) = GetWindowKeys(now);
        var userQuests = await _db.UserQuests
            .Include(userQuest => userQuest.Quest)
            .Where(userQuest =>
                userQuest.UserId == userId &&
                userQuest.Quest != null &&
                userQuest.Quest.IsActive &&
                ((userQuest.Quest.Type == QuestType.Daily && userQuest.WindowKey == dailyKey) ||
                 (userQuest.Quest.Type == QuestType.Weekly && userQuest.WindowKey == weeklyKey)))
            .OrderBy(userQuest => userQuest.ClaimedAt != null)
            .ThenBy(userQuest => userQuest.IsCompleted)
            .ThenBy(userQuest => userQuest.Quest!.Type)
            .ThenBy(userQuest => userQuest.Quest!.Title)
            .ToListAsync();

        return userQuests.Select(MapQuestResponse).ToList();
    }

    public async Task UpdateProgressAsync(Guid userId, string eventType, string? priority = null)
    {
        var now = DateTime.UtcNow;
        await EnsureActiveQuestsAsync(userId, now);

        var (dailyKey, weeklyKey) = GetWindowKeys(now);
        var userQuests = await _db.UserQuests
            .Include(userQuest => userQuest.Quest)
            .Where(userQuest =>
                userQuest.UserId == userId &&
                userQuest.Quest != null &&
                userQuest.Quest.IsActive &&
                userQuest.ClaimedAt == null &&
                ((userQuest.Quest.Type == QuestType.Daily && userQuest.WindowKey == dailyKey) ||
                 (userQuest.Quest.Type == QuestType.Weekly && userQuest.WindowKey == weeklyKey)))
            .ToListAsync();

        var streakDays = await _db.UserGlobalXp
            .Where(user => user.UserId == userId)
            .Select(user => user.StreakDays)
            .FirstOrDefaultAsync();

        var changed = false;

        foreach (var userQuest in userQuests)
        {
            if (userQuest.Quest == null)
            {
                continue;
            }

            try
            {
                // Условие Requirement хранится как JSON, чтобы каталог квестов оставался расширяемым
                // без отдельной миграции на каждую новую механику.
                var requirement = JsonSerializer.Deserialize<JsonElement>(userQuest.Quest.Requirement);
                var requirementType = requirement.GetProperty("type").GetString();
                var requiredCount = requirement.TryGetProperty("count", out var countElement)
                    ? Math.Max(1, countElement.GetInt32())
                    : 1;

                if (requirementType == "maintain_streak")
                {
                    // Квесты на streak не накапливаются по событиям.
                    // Их прогресс отражает текущее состояние streak и пересчитывается из проекции пользователя.
                    var nextProgress = Math.Min(requiredCount, streakDays);
                    if (userQuest.Progress != nextProgress)
                    {
                        userQuest.Progress = nextProgress;
                        changed = true;
                    }

                    if (!userQuest.IsCompleted && userQuest.Progress >= requiredCount)
                    {
                        userQuest.IsCompleted = true;
                        userQuest.CompletedAt = DateTime.UtcNow;
                        changed = true;
                    }

                    continue;
                }

                if (!MatchesEvent(requirementType, eventType, priority))
                {
                    continue;
                }

                // Обычные event-driven квесты прогрессируют дискретно: одно доменное событие даёт один шаг.
                var nextValue = Math.Min(requiredCount, userQuest.Progress + 1);
                if (nextValue != userQuest.Progress)
                {
                    userQuest.Progress = nextValue;
                    changed = true;
                }

                if (!userQuest.IsCompleted && userQuest.Progress >= requiredCount)
                {
                    userQuest.IsCompleted = true;
                    userQuest.CompletedAt = DateTime.UtcNow;
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update quest progress for quest {QuestId}", userQuest.QuestId);
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }
    }

    public async Task<RewardEventResponse?> ClaimQuestAsync(Guid userId, Guid questId)
    {
        var userQuest = await _db.UserQuests
            .Include(userQuest => userQuest.Quest)
            .FirstOrDefaultAsync(userQuest => userQuest.UserId == userId && userQuest.QuestId == questId);

        if (userQuest == null || userQuest.Quest == null || !userQuest.IsCompleted || userQuest.ClaimedAt != null)
        {
            return null;
        }

        // Награда квеста начисляется отдельно от обычной event-награды.
        // Здесь мы сознательно повторно используем streak multiplier, чтобы длинная серия усиливала и квестовую награду.
        await _xpService.EnsureUserExistsAsync(userId);

        var global = await _db.UserGlobalXp.FirstAsync(user => user.UserId == userId);
        var streak = await _streakService.GetStreakAsync(userId);
        var multiplier = _streakService.GetStreakMultiplier(streak.StreakDays);
        var xpAward = (long)Math.Round(userQuest.Quest.XpReward * multiplier);
        var coinAward = userQuest.Quest.CoinReward;

        global.TotalXp += xpAward;
        global.Coins += coinAward;

        var previousLevel = global.Level;
        global.Level = XpService.CalculateLevel(global.TotalXp);
        global.UpdatedAt = DateTime.UtcNow;

        _db.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            Amount = xpAward,
            CoinsAmount = coinAward,
            Reason = XpReason.QuestCompleted,
            Status = "Applied",
            Metadata = JsonSerializer.Serialize(new
            {
                questId,
                questTitle = userQuest.Quest.Title,
                multiplier,
                userQuest.WindowKey
            })
        });

        userQuest.ClaimedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        // После выдачи награды leaderboard надо обновить немедленно, потому что квесты могут менять топ.
        await _leaderboardService.RefreshGlobalLeaderboardAsync();

        return new RewardEventResponse
        {
            XpAwarded = xpAward,
            CoinsAwarded = coinAward,
            NewLevel = global.Level,
            LeveledUp = global.Level > previousLevel,
            Reason = "QuestCompleted",
            RewardApplied = true,
            CountedTowardsQuestProgress = false
        };
    }

    public Task AssignDailyQuestsAsync(Guid userId)
        => EnsureActiveQuestsAsync(userId, DateTime.UtcNow);

    private async Task EnsureActiveQuestsAsync(Guid userId, DateTime now)
    {
        // Квесты не создаются заранее для всех пользователей.
        // Вместо этого сервис лениво материализует записи в момент первого запроса или первого события в окне.
        var activeQuests = await _db.Quests
            .Where(quest => quest.IsActive && (quest.Type == QuestType.Daily || quest.Type == QuestType.Weekly))
            .ToListAsync();

        if (activeQuests.Count == 0)
        {
            return;
        }

        var existingQuests = await _db.UserQuests
            .Where(userQuest => userQuest.UserId == userId)
            .ToListAsync();

        var added = false;
        var changed = false;

        foreach (var quest in activeQuests)
        {
            // Ключ WindowKey отделяет ежедневные и недельные выдачи друг от друга.
            // Один и тот же Quest может появляться у пользователя многократно, но с разными окнами.
            var windowKey = BuildWindowKey(quest.Type, now);
            var existingQuest = existingQuests.FirstOrDefault(userQuest => userQuest.QuestId == quest.Id);
            if (existingQuest is null)
            {
                _db.UserQuests.Add(new UserQuest
                {
                    UserId = userId,
                    QuestId = quest.Id,
                    WindowKey = windowKey,
                    AssignedAt = now
                });

                added = true;
                continue;
            }

            if (string.Equals(existingQuest.WindowKey, windowKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            existingQuest.WindowKey = windowKey;
            existingQuest.Progress = 0;
            existingQuest.IsCompleted = false;
            existingQuest.AssignedAt = now;
            existingQuest.CompletedAt = null;
            existingQuest.ClaimedAt = null;
            changed = true;
        }

        if (added || changed)
        {
            await _db.SaveChangesAsync();
        }
    }

    private async Task RefreshWindowQuestStatesAsync(Guid userId, DateTime now)
    {
        // Этот проход нужен для квестов, чей прогресс зависит не от входящего eventType,
        // а от уже накопленного состояния пользователя, например от текущего streak.
        var (_, weeklyKey) = GetWindowKeys(now);
        var streakDays = await _db.UserGlobalXp
            .Where(user => user.UserId == userId)
            .Select(user => user.StreakDays)
            .FirstOrDefaultAsync();

        var weeklyStreakQuests = await _db.UserQuests
            .Include(userQuest => userQuest.Quest)
            .Where(userQuest =>
                userQuest.UserId == userId &&
                userQuest.Quest != null &&
                userQuest.Quest.Type == QuestType.Weekly &&
                userQuest.WindowKey == weeklyKey &&
                userQuest.ClaimedAt == null)
            .ToListAsync();

        var changed = false;

        foreach (var userQuest in weeklyStreakQuests)
        {
            try
            {
                var requirement = JsonSerializer.Deserialize<JsonElement>(userQuest.Quest!.Requirement);
                var requirementType = requirement.GetProperty("type").GetString();
                if (requirementType != "maintain_streak")
                {
                    continue;
                }

                var requiredCount = requirement.TryGetProperty("count", out var countElement)
                    ? Math.Max(1, countElement.GetInt32())
                    : 1;

                var nextProgress = Math.Min(requiredCount, streakDays);
                if (userQuest.Progress != nextProgress)
                {
                    userQuest.Progress = nextProgress;
                    changed = true;
                }

                if (!userQuest.IsCompleted && userQuest.Progress >= requiredCount)
                {
                    userQuest.IsCompleted = true;
                    userQuest.CompletedAt = DateTime.UtcNow;
                    changed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh weekly quest state for quest {QuestId}", userQuest.QuestId);
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }
    }

    private static QuestResponse MapQuestResponse(UserQuest userQuest)
    {
        var requiredCount = 1;

        try
        {
            var requirement = JsonSerializer.Deserialize<JsonElement>(userQuest.Quest!.Requirement);
            if (requirement.TryGetProperty("count", out var countElement))
            {
                requiredCount = Math.Max(1, countElement.GetInt32());
            }
        }
        catch
        {
            // Если metadata квеста повреждена, API всё равно возвращает безопасный минимум,
            // чтобы пользователь видел квест, а проблема диагностировалась отдельно.
        }

        return new QuestResponse
        {
            Id = userQuest.QuestId,
            Type = userQuest.Quest!.Type.ToString(),
            Title = userQuest.Quest.Title,
            Description = userQuest.Quest.Description,
            XpReward = userQuest.Quest.XpReward,
            CoinReward = userQuest.Quest.CoinReward,
            Progress = userQuest.Progress,
            RequiredCount = requiredCount,
            IsCompleted = userQuest.IsCompleted,
            IsClaimed = userQuest.ClaimedAt != null
        };
    }

    // Здесь сосредоточено сопоставление внутреннего eventType с типами требований каталога квестов.
    // Контракт должен оставаться явным: новые типы событий добавляются осознанно, а не «магически».
    private static bool MatchesEvent(string? requirementType, string eventType, string? priority)
        => requirementType switch
        {
            "complete_tasks" => eventType == "task-completed",
            "add_comments" => eventType == "comment-added",
            "complete_high_priority" => eventType == "task-completed" &&
                (string.Equals(priority, "high", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(priority, "critical", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };

    private static (string dailyKey, string weeklyKey) GetWindowKeys(DateTime now)
        => (BuildWindowKey(QuestType.Daily, now), BuildWindowKey(QuestType.Weekly, now));

    private static string BuildWindowKey(QuestType questType, DateTime now)
        => questType switch
        {
            QuestType.Daily => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            QuestType.Weekly => $"{ISOWeek.GetYear(now):0000}-W{ISOWeek.GetWeekOfYear(now):00}",
            _ => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
}
