using StackExchange.Redis;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Responses;
using MessengerService.Infrastructure.Data;

namespace MessengerService.Application.Services;

public class PresenceService : IPresenceService
{
    private readonly MessengerRedisConnectionAccessor _redisAccessor;
    private readonly ILogger<PresenceService> _logger;
    private const string OnlineKeyPrefix = "messenger:presence:online:";
    private const string TypingKeyPrefix = "messenger:presence:typing:";
    private const string LastSeenKeyPrefix = "messenger:presence:lastseen:";
    private static readonly TimeSpan OnlineTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TypingTtl = TimeSpan.FromSeconds(5);

    public PresenceService(MessengerRedisConnectionAccessor redisAccessor, ILogger<PresenceService> logger)
    {
        _redisAccessor = redisAccessor;
        _logger = logger;
    }

    public async Task SetOnlineAsync(Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        await db.StringSetAsync($"{OnlineKeyPrefix}{userId}", DateTime.UtcNow.ToString("O"), OnlineTtl);
    }

    public async Task SetOfflineAsync(Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        // Сначала сохраняем last-seen, затем убираем online-key из Redis.
        await db.StringSetAsync($"{LastSeenKeyPrefix}{userId}", DateTime.UtcNow.ToString("O"));
        await db.KeyDeleteAsync($"{OnlineKeyPrefix}{userId}");
    }

    public async Task<bool> IsOnlineAsync(Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        return await db.KeyExistsAsync($"{OnlineKeyPrefix}{userId}");
    }

    public async Task<IReadOnlyList<PresenceResponse>> GetPresenceAsync(IEnumerable<Guid> userIds)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        var results = new List<PresenceResponse>();

        foreach (var userId in userIds)
        {
            var isOnline = await db.KeyExistsAsync($"{OnlineKeyPrefix}{userId}");
            DateTime? lastSeen = null;

            if (!isOnline)
            {
                var lastSeenStr = await db.StringGetAsync($"{LastSeenKeyPrefix}{userId}");
                if (lastSeenStr.HasValue && DateTime.TryParse(lastSeenStr!, out var parsed))
                    lastSeen = parsed;
            }

            results.Add(new PresenceResponse
            {
                UserId = userId,
                IsOnline = isOnline,
                LastSeenAt = isOnline ? DateTime.UtcNow : lastSeen
            });
        }

        return results;
    }

    public async Task SetTypingAsync(Guid userId, string chatId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        await db.StringSetAsync($"{TypingKeyPrefix}{chatId}:{userId}", "1", TypingTtl);
    }

    public async Task ClearTypingAsync(Guid userId, string chatId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        await db.KeyDeleteAsync($"{TypingKeyPrefix}{chatId}:{userId}");
    }

    public async Task<IReadOnlyList<Guid>> GetTypingUsersAsync(string chatId)
    {
        var server = _redisAccessor.Connection.GetServer(_redisAccessor.Connection.GetEndPoints().First());
        var pattern = $"{TypingKeyPrefix}{chatId}:*";
        var keys = server.Keys(pattern: pattern).ToList();

        var typingUsers = new List<Guid>();
        foreach (var key in keys)
        {
            var userIdStr = key.ToString().Split(':').Last();
            if (Guid.TryParse(userIdStr, out var userId))
                typingUsers.Add(userId);
        }

        return typingUsers;
    }
}
