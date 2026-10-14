using StackExchange.Redis;
using CallService.Application.Interfaces;
using CallService.Infrastructure.Data;

namespace CallService.Infrastructure.Services;

public class RedisCallParticipantTracker : ICallParticipantTracker
{
    private readonly CallRedisConnectionAccessor _redisAccessor;
    private const string CallParticipantsKey = "calls:participants:";
    private const string UserActiveCallKey = "calls:user-active:";

    public RedisCallParticipantTracker(CallRedisConnectionAccessor redisAccessor)
    {
        _redisAccessor = redisAccessor;
    }

    public async Task AddToCallAsync(string callId, Guid userId, string connectionId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        await db.HashSetAsync($"{CallParticipantsKey}{callId}", userId.ToString(), connectionId);
        await db.StringSetAsync($"{UserActiveCallKey}{userId}", callId, TimeSpan.FromHours(4));
    }

    public async Task RemoveFromCallAsync(string callId, Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        await db.HashDeleteAsync($"{CallParticipantsKey}{callId}", userId.ToString());
        await db.KeyDeleteAsync($"{UserActiveCallKey}{userId}");
    }

    public async Task<IReadOnlyList<Guid>> GetActiveParticipantsAsync(string callId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        var entries = await db.HashGetAllAsync($"{CallParticipantsKey}{callId}");
        return entries
            .Select(e => Guid.TryParse(e.Name.ToString(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList();
    }

    public async Task<string?> GetUserConnectionAsync(string callId, Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        var value = await db.HashGetAsync($"{CallParticipantsKey}{callId}", userId.ToString());
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<string?> GetUserActiveCallAsync(Guid userId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        var value = await db.StringGetAsync($"{UserActiveCallKey}{userId}");
        return value.HasValue ? value.ToString() : null;
    }

    public async Task ClearCallAsync(string callId)
    {
        var db = _redisAccessor.Connection.GetDatabase();
        var entries = await db.HashGetAllAsync($"{CallParticipantsKey}{callId}");
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry.Name.ToString(), out var userId))
                await db.KeyDeleteAsync($"{UserActiveCallKey}{userId}");
        }
        await db.KeyDeleteAsync($"{CallParticipantsKey}{callId}");
    }
}
