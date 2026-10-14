using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;

namespace GamificationService.Application.Services;

/// <summary>
/// Центральный сервис экономики прогресса.
/// Он превращает доменные события в XP- и coin-транзакции, поддерживает глобальный и проектный прогресс
/// и гарантирует, что откат события оставит обе проекции в согласованном состоянии.
/// </summary>
public class XpService : IXpService
{
    private readonly GamificationDbContext _db;
    private readonly IStreakService _streakService;
    private readonly ILogger<XpService> _logger;

    public XpService(GamificationDbContext db, IStreakService streakService, ILogger<XpService> logger)
    {
        _db = db;
        _streakService = streakService;
        _logger = logger;
    }

    public async Task EnsureUserExistsAsync(Guid userId)
    {
        var exists = await _db.UserGlobalXp.AnyAsync(u => u.UserId == userId);
        if (!exists)
        {
            // Базовая проекция создаётся лениво при первом игровом событии,
            // чтобы сервис не зависел от отдельного provisioning-потока пользователей.
            _db.UserGlobalXp.Add(new UserGlobalXp { UserId = userId });
            await _db.SaveChangesAsync();
        }
    }

    public async Task<RewardEventResponse> AwardXpAsync(Guid userId, Guid? projectId, XpReason reason, Guid? taskId = null, string? priority = null)
    {
        await EnsureUserExistsAsync(userId);

        if (reason == XpReason.CommentAdded)
        {
            var noOpResponse = await TryCreateDuplicateCommentRewardAsync(userId, taskId, reason);
            if (noOpResponse is not null)
            {
                return noOpResponse;
            }
        }

        if (reason == XpReason.TaskCompleted)
        {
            // Серия активности обновляется только на завершении задач.
            // Так streak остаётся привязанным к полезной активности, а не к любому побочному действию.
            await _streakService.RecordActivityAsync(userId);
        }

        // Сначала вычисляем базовую награду, затем накладываем streak multiplier.
        // К монетам multiplier не применяется, чтобы экономика магазина оставалась предсказуемой.
        var (baseXp, baseCoins) = CalculateBaseReward(reason, priority);
        var streak = await _streakService.GetStreakAsync(userId);
        var multiplier = _streakService.GetStreakMultiplier(streak.StreakDays);
        var finalXp = (long)Math.Round(baseXp * multiplier);
        var finalCoins = baseCoins;

        // Сначала фиксируем сам факт награды как транзакцию.
        // Поле Metadata хранит диагностический контекст, который нужен для аудита и обратного разбора начисления.
        _db.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            ProjectId = projectId,
            Amount = finalXp,
            CoinsAmount = finalCoins,
            Reason = reason,
            TaskId = taskId,
            Status = "Applied",
            Metadata = JsonSerializer.Serialize(new
            {
                priority,
                multiplier,
                streakDays = streak.StreakDays
            })
        });

        // Глобальная проекция — каноническое накопление общего прогресса пользователя по всей системе.
        var global = await _db.UserGlobalXp.FirstAsync(u => u.UserId == userId);
        global.TotalXp += finalXp;
        global.Coins += finalCoins;
        global.UpdatedAt = DateTime.UtcNow;

        var oldLevel = global.Level;
        global.Level = CalculateLevel(global.TotalXp);
        var leveledUp = global.Level > oldLevel;

        // Проектная проекция обновляется только если событие связано с projectId.
        // Она нужна для локального прогресса внутри конкретного пространства работы и отдельных leaderboard.
        if (projectId.HasValue)
        {
            var projectXp = await _db.UserProjectXp
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectId == projectId.Value);

            if (projectXp == null)
            {
                projectXp = new UserProjectXp { UserId = userId, ProjectId = projectId.Value };
                _db.UserProjectXp.Add(projectXp);
            }

            projectXp.Xp += finalXp;
            if (reason == XpReason.TaskCompleted) projectXp.TasksCompleted++;
            projectXp.Level = CalculateLevel(projectXp.Xp);
            projectXp.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Awarded {Xp} XP and {Coins} coins to user {UserId} for {Reason}",
            finalXp, finalCoins, userId, reason);

        return new RewardEventResponse
        {
            XpAwarded = finalXp,
            CoinsAwarded = finalCoins,
            NewLevel = global.Level,
            LeveledUp = leveledUp,
            Reason = reason.ToString(),
            RewardApplied = true,
            CountedTowardsQuestProgress = true
        };
    }

    public async Task<RewardEventResponse> RollbackXpAsync(Guid userId, Guid? projectId, Guid taskId)
    {
        await EnsureUserExistsAsync(userId);

        // Ищем последнее начисление за конкретную задачу, чтобы откатиться от фактически применённой награды,
        // а не пытаться пересчитать её повторно из текущих правил.
        var originalTx = await _db.XpTransactions
            .Where(t => t.UserId == userId && t.TaskId == taskId && t.Reason == XpReason.TaskCompleted)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (originalTx == null)
        {
            var globalProjection = await _db.UserGlobalXp.FirstAsync(u => u.UserId == userId);
            return new RewardEventResponse
            {
                NewLevel = globalProjection.Level,
                Reason = "TaskReopened",
                RewardApplied = false,
                CountedTowardsQuestProgress = false
            };
        }

        var rollbackXp = -originalTx.Amount;
        var rollbackCoins = -originalTx.CoinsAmount;

        _db.XpTransactions.Add(new XpTransaction
        {
            UserId = userId,
            ProjectId = projectId,
            Amount = rollbackXp,
            CoinsAmount = rollbackCoins,
            Reason = XpReason.TaskReopened,
            TaskId = taskId,
            Status = "RolledBack",
            Metadata = JsonSerializer.Serialize(new
            {
                sourceTransactionId = originalTx.Id,
                originalAmount = originalTx.Amount,
                originalCoins = originalTx.CoinsAmount
            })
        });

        var global = await _db.UserGlobalXp.FirstAsync(u => u.UserId == userId);
        global.TotalXp = Math.Max(0, global.TotalXp + rollbackXp);
        global.Coins = Math.Max(0, global.Coins + rollbackCoins);
        global.Level = CalculateLevel(global.TotalXp);
        global.UpdatedAt = DateTime.UtcNow;

        if (projectId.HasValue)
        {
            var projectXp = await _db.UserProjectXp
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectId == projectId.Value);
            if (projectXp != null)
            {
                projectXp.Xp = Math.Max(0, projectXp.Xp + rollbackXp);
                projectXp.TasksCompleted = Math.Max(0, projectXp.TasksCompleted - 1);
                projectXp.Level = CalculateLevel(projectXp.Xp);
                projectXp.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        return new RewardEventResponse
        {
            XpAwarded = rollbackXp,
            CoinsAwarded = rollbackCoins,
            NewLevel = global.Level,
            LeveledUp = false,
            Reason = "TaskReopened",
            RewardApplied = true,
            CountedTowardsQuestProgress = false
        };
    }

    public async Task<UserXpResponse> GetUserGlobalXpAsync(Guid userId)
    {
        await EnsureUserExistsAsync(userId);
        var global = await _db.UserGlobalXp.FirstAsync(u => u.UserId == userId);
        return new UserXpResponse
        {
            UserId = userId,
            TotalXp = global.TotalXp,
            Level = global.Level,
            XpToNextLevel = XpForLevel(global.Level + 1) - global.TotalXp,
            Coins = global.Coins,
            StreakDays = global.StreakDays,
            FreezeCount = global.FreezeCount
        };
    }

    public async Task<UserProjectXpResponse> GetUserProjectXpAsync(Guid userId, Guid projectId)
    {
        var projectXp = await _db.UserProjectXp
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ProjectId == projectId);

        if (projectXp == null)
            return new UserProjectXpResponse { UserId = userId, ProjectId = projectId, Level = 1, XpToNextLevel = XpForLevel(2) };

        return new UserProjectXpResponse
        {
            UserId = userId,
            ProjectId = projectId,
            Xp = projectXp.Xp,
            Level = projectXp.Level,
            XpToNextLevel = XpForLevel(projectXp.Level + 1) - projectXp.Xp,
            TasksCompleted = projectXp.TasksCompleted
        };
    }

    public async Task<IEnumerable<XpHistoryItemResponse>> GetXpHistoryAsync(Guid userId, int limit = 50, int offset = 0)
    {
        return await _db.XpTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset).Take(limit)
            .Select(t => new XpHistoryItemResponse
            {
                Id = t.Id,
                Amount = t.Amount,
                CoinsAmount = t.CoinsAmount,
                Reason = t.Reason.ToString(),
                Status = t.Status,
                Metadata = t.Metadata,
                TaskId = t.TaskId,
                ProjectId = t.ProjectId,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();
    }

    // Расчётные функции вынесены отдельно от orchestration-логики,
    // чтобы правила экономики можно было менять независимо от побочных эффектов и записи в БД.
    private static (long xp, long coins) CalculateBaseReward(XpReason reason, string? priority)
    {
        var priorityMultiplier = priority?.ToLowerInvariant() switch
        {
            "low" => 0.5,
            "medium" => 1.0,
            "high" => 1.5,
            "critical" => 2.0,
            _ => 1.0
        };

        return reason switch
        {
            XpReason.TaskCompleted => ((long)Math.Round(10 * priorityMultiplier), (long)Math.Round(5 * priorityMultiplier)),
            XpReason.SubtaskCompleted => (3, 1),
            XpReason.CommentAdded => (2, 0),
            XpReason.QuestCompleted => (0, 0), // Награды за квесты начисляются отдельным потоком в QuestService.
            _ => (0, 0)
        };
    }

    private async Task<RewardEventResponse?> TryCreateDuplicateCommentRewardAsync(Guid userId, Guid? taskId, XpReason reason)
    {
        if (!taskId.HasValue)
        {
            return null;
        }

        var windowStart = DateTime.UtcNow.Date;
        var windowEnd = windowStart.AddDays(1);
        var duplicateExists = await _db.XpTransactions.AnyAsync(transaction =>
            transaction.UserId == userId &&
            transaction.TaskId == taskId.Value &&
            transaction.Reason == XpReason.CommentAdded &&
            transaction.CreatedAt >= windowStart &&
            transaction.CreatedAt < windowEnd &&
            transaction.Status == "Applied");

        if (!duplicateExists)
        {
            return null;
        }

        var global = await _db.UserGlobalXp.FirstAsync(user => user.UserId == userId);
        _logger.LogInformation(
            "Skipped duplicate comment reward for user {UserId}, task {TaskId} and UTC day {WindowStart:yyyy-MM-dd}",
            userId,
            taskId.Value,
            windowStart);

        return new RewardEventResponse
        {
            XpAwarded = 0,
            CoinsAwarded = 0,
            NewLevel = global.Level,
            LeveledUp = false,
            Reason = reason.ToString(),
            RewardApplied = false,
            CountedTowardsQuestProgress = false
        };
    }

    public static int CalculateLevel(long totalXp)
    {
        // Пока уровень считается линейно от общего XP.
        // Функция вынесена в единое место, чтобы одинаково использоваться в API, leaderboard и откатах.
        if (totalXp < XpForLevel(2))
        {
            return 1;
        }

        return Math.Max(2, (int)(totalXp / 100L));
    }

    public static long XpForLevel(int level) => level <= 1 ? 0 : level * 100L;
}
