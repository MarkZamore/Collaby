using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Constants;
using GamificationService.Domain.Entities;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Data;

namespace GamificationService.Application.Services;

/// <summary>
/// Собирает глобальные и проектные leaderboard.
/// Источником истины остаётся PostgreSQL, а Redis используется как ускоряющий кеш sorted set-ов.
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    private const string GlobalLeaderboardKey = "gamification:leaderboard:global";
    private const string ProjectLeaderboardKeyPrefix = "gamification:leaderboard:project:";
    private readonly GamificationDbContext _db;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IUserLookupClient _userLookupClient;
    private readonly ILogger<LeaderboardService> _logger;

    public LeaderboardService(
        GamificationDbContext db,
        IServiceProvider serviceProvider,
        IUserLookupClient userLookupClient,
        ILogger<LeaderboardService> logger)
    {
        _db = db;
        _redis = serviceProvider.GetService<IConnectionMultiplexer>();
        _userLookupClient = userLookupClient;
        _logger = logger;
    }

    public async Task<IEnumerable<LeaderboardEntryResponse>> GetGlobalTopAsync(int count = 10)
    {
        // Сначала пытаемся отдать уже подготовленный sorted set из Redis.
        // Это самый дешёвый путь для frequently-read leaderboard API.
        if (_redis != null)
        {
            try
            {
                var redisDb = _redis.GetDatabase();
                var cached = await redisDb.SortedSetRangeByScoreWithScoresAsync(GlobalLeaderboardKey,
                    order: Order.Descending, take: count);
                if (cached.Length > 0)
                {
                    var cachedEntries = cached.Select((entry, index) => new LeaderboardEntryResponse
                    {
                        Rank = index + 1,
                        UserId = Guid.Parse(entry.Element.ToString()),
                        Xp = (long)entry.Score,
                        Level = XpService.CalculateLevel((long)entry.Score)
                    }).ToList();

                    await EnrichEntriesAsync(cachedEntries);
                    return cachedEntries;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis leaderboard cache unavailable, falling back to DB");
            }
        }

        // Если кеш пустой или Redis недоступен, строим ответ из PostgreSQL.
        // Именно этот путь остаётся гарантированным источником данных.
        var topUsers = await _db.UserGlobalXp
            .OrderByDescending(u => u.TotalXp)
            .Take(count)
            .ToListAsync();

        var entries = topUsers.Select((u, i) => new LeaderboardEntryResponse
        {
            Rank = i + 1,
            UserId = u.UserId,
            Xp = u.TotalXp,
            Level = u.Level
        }).ToList();

        await EnrichEntriesAsync(entries);
        return entries;
    }

    public async Task<IEnumerable<LeaderboardEntryResponse>> GetProjectTopAsync(Guid projectId, int count = 10)
    {
        // Проектный leaderboard использует отдельный namespace в Redis,
        // чтобы кэширование не смешивало разные проекты между собой.
        if (_redis != null)
        {
            try
            {
                var redisDb = _redis.GetDatabase();
                var cached = await redisDb.SortedSetRangeByScoreWithScoresAsync($"{ProjectLeaderboardKeyPrefix}{projectId}",
                    order: Order.Descending, take: count);
                if (cached.Length > 0)
                {
                    var cachedEntries = cached.Select((entry, index) => new LeaderboardEntryResponse
                    {
                        Rank = index + 1,
                        UserId = Guid.Parse(entry.Element.ToString()),
                        Xp = (long)entry.Score,
                        Level = XpService.CalculateLevel((long)entry.Score)
                    }).ToList();

                    await EnrichEntriesAsync(cachedEntries);
                    return cachedEntries;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis project leaderboard cache unavailable");
            }
        }

        var topUsers = await _db.UserProjectXp
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.Xp)
            .Take(count)
            .ToListAsync();

        var entries = topUsers.Select((u, i) => new LeaderboardEntryResponse
        {
            Rank = i + 1,
            UserId = u.UserId,
            Xp = u.Xp,
            Level = u.Level
        }).ToList();

        await EnrichEntriesAsync(entries);
        return entries;
    }

    public async Task RefreshGlobalLeaderboardAsync()
    {
        if (_redis == null) return;
        var redisDb = _redis.GetDatabase();
        // Проще и надёжнее пересобрать срез целиком, чем поддерживать сложные частичные обновления
        // при каждом типе транзакции.
        await redisDb.KeyDeleteAsync(GlobalLeaderboardKey);

        var allUsers = await _db.UserGlobalXp.Where(u => u.TotalXp > 0).ToListAsync();
        var entries = allUsers.Select(u => new SortedSetEntry(u.UserId.ToString(), u.TotalXp)).ToArray();
        if (entries.Length > 0)
            await redisDb.SortedSetAddAsync(GlobalLeaderboardKey, entries);
    }

    public async Task RefreshProjectLeaderboardAsync(Guid projectId)
    {
        if (_redis == null) return;
        var redisDb = _redis.GetDatabase();
        var key = $"{ProjectLeaderboardKeyPrefix}{projectId}";
        // Проектный кеш обновляется отдельно, чтобы локальные изменения не инвалидировали глобальный топ.
        await redisDb.KeyDeleteAsync(key);

        var users = await _db.UserProjectXp.Where(p => p.ProjectId == projectId && p.Xp > 0).ToListAsync();
        var entries = users.Select(u => new SortedSetEntry(u.UserId.ToString(), u.Xp)).ToArray();
        if (entries.Length > 0)
            await redisDb.SortedSetAddAsync(key, entries);
    }

    public async Task CreateAnnualSnapshotAsync(int year)
    {
        // Годовой snapshot нужен как замороженная историческая витрина.
        // Он отделён от текущего leaderboard и не должен переписываться повторным запуском job.
        var existingSnapshot = await _db.LeaderboardSnapshots
            .AsNoTracking()
            .AnyAsync(snapshot => snapshot.Type == LeaderboardType.Global && snapshot.Year == year);

        if (existingSnapshot)
        {
            _logger.LogInformation("Annual leaderboard snapshot for year {Year} already exists. Skipping.", year);
            return;
        }

        var topUsers = await _db.UserGlobalXp.OrderByDescending(u => u.TotalXp).Take(10).ToListAsync();

        var snapshots = topUsers.Select((u, i) => new LeaderboardSnapshot
        {
            Type = LeaderboardType.Global,
            Year = year,
            Rank = i + 1,
            UserId = u.UserId,
            Xp = u.TotalXp
        });

        _db.LeaderboardSnapshots.AddRange(snapshots);
        await EnsureAnnualBadgeAwardsAsync(topUsers);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created annual leaderboard snapshot for year {Year} with {Count} entries", year, topUsers.Count);
    }

    private async Task EnsureAnnualBadgeAwardsAsync(IReadOnlyList<UserGlobalXp> topUsers)
    {
        // Ежегодные бейджи выдаются только один раз.
        // Повторный расчёт snapshot не должен плодить дубли в инвентаре.
        var badgeAwards = new[]
        {
            new { Rank = 1, ItemId = GamificationCatalog.GoldCrownBadgeId },
            new { Rank = 2, ItemId = GamificationCatalog.SilverCrownBadgeId },
            new { Rank = 3, ItemId = GamificationCatalog.BronzeCrownBadgeId }
        };

        foreach (var award in badgeAwards)
        {
            if (topUsers.Count < award.Rank)
            {
                continue;
            }

            var user = topUsers[award.Rank - 1];
            var alreadyOwned = await _db.UserInventory.AnyAsync(inventory =>
                inventory.UserId == user.UserId &&
                inventory.ItemId == award.ItemId);

            if (alreadyOwned)
            {
                continue;
            }

            _db.UserInventory.Add(new UserInventory
            {
                UserId = user.UserId,
                ItemId = award.ItemId,
                IsEquipped = false
            });
        }
    }

    private async Task EnrichEntriesAsync(List<LeaderboardEntryResponse> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        // Лидерборд хранит только игровые данные.
        // Username и avatar подтягиваются отдельным обогащением из UserService.
        IReadOnlyDictionary<Guid, UserLookupResult> userLookup;
        try
        {
            userLookup = await _userLookupClient.GetUsersByIdsAsync(entries.Select(entry => entry.UserId).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserService unavailable during leaderboard enrichment. Returning leaderboard without profile fields.");
            return;
        }

        foreach (var entry in entries)
        {
            if (!userLookup.TryGetValue(entry.UserId, out var user))
            {
                continue;
            }

            entry.Username = user.Username;
            entry.AvatarUrl = user.AvatarUrl;
        }
    }
}
