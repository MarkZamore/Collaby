using MongoDB.Driver;
using CallService.Application.Interfaces;
using CallService.Contracts.Requests;
using CallService.Contracts.Responses;
using CallService.Domain.Entities;
using CallService.Infrastructure.Data;

namespace CallService.Application.Services;

public class CallSessionService : ICallService
{
    private readonly IMongoCollection<CallSession> _calls;
    private readonly ICallParticipantTracker _tracker;
    private readonly IUserLookupClient _userLookup;
    private readonly IMessengerClient _messengerClient;
    private readonly ILogger<CallSessionService> _logger;

    public CallSessionService(
        CallMongoDatabaseAccessor databaseAccessor,
        ICallParticipantTracker tracker,
        IUserLookupClient userLookup,
        IMessengerClient messengerClient,
        ILogger<CallSessionService> logger)
    {
        var database = databaseAccessor.Database;
        _calls = database.GetCollection<CallSession>("callSessions");
        _tracker = tracker;
        _userLookup = userLookup;
        _messengerClient = messengerClient;
        _logger = logger;
    }

    public async Task<CallSessionResponse> StartCallAsync(Guid initiatorId, StartCallRequest request, CancellationToken ct = default)
    {
        var participants = new List<CallParticipant>
        {
            new()
            {
                UserId = initiatorId,
                Status = "joined",
                JoinedAt = DateTime.UtcNow,
                HasVideo = request.Type == "video",
                HasAudio = true
            }
        };

        foreach (var participantId in request.ParticipantIds.Where(id => id != initiatorId))
        {
            participants.Add(new CallParticipant
            {
                UserId = participantId,
                Status = "ringing"
            });
        }

        var call = new CallSession
        {
            ChatId = request.ChatId,
            Type = request.Type,
            Status = "ringing",
            InitiatorId = initiatorId,
            IsGroup = request.ParticipantIds.Count > 1,
            Participants = participants,
            StartedAt = DateTime.UtcNow
        };

        await _calls.InsertOneAsync(call, cancellationToken: ct);

        // После старта звонка публикуем system message в связанный чат.
        if (!string.IsNullOrEmpty(request.ChatId))
        {
            var callTypeLabel = request.Type == "video" ? "видеозвонок" : "аудиозвонок";
            try
            {
                await _messengerClient.SendSystemMessageAsync(request.ChatId, $"📞 Начат {callTypeLabel}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send system message to chat {ChatId}", request.ChatId);
            }
        }

        return await MapToResponseAsync(call, ct);
    }

    public async Task<CallSessionResponse> AnswerCallAsync(string callId, Guid userId, AnswerCallRequest request, CancellationToken ct = default)
    {
        var call = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        if (call == null)
            throw new ArgumentException("Call not found");

        var participant = call.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
            throw new UnauthorizedAccessException("Not a participant of this call");

        var update = Builders<CallSession>.Update
            .Set("participants.$.status", "joined")
            .Set("participants.$.joinedAt", DateTime.UtcNow)
            .Set("participants.$.hasVideo", request.HasVideo)
            .Set("participants.$.hasAudio", request.HasAudio);

        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );

        await _calls.UpdateOneAsync(filter, update, cancellationToken: ct);

        // Если это первый ответивший после инициатора, переводим звонок в активное состояние.
        if (call.Status == "ringing")
        {
            await _calls.UpdateOneAsync(
                c => c.Id == callId,
                Builders<CallSession>.Update.Set(c => c.Status, "active"),
                cancellationToken: ct);
        }

        var updated = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        return await MapToResponseAsync(updated!, ct);
    }

    public async Task<CallSessionResponse> DeclineCallAsync(string callId, Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );

        var update = Builders<CallSession>.Update
            .Set("participants.$.status", "declined")
            .Set("participants.$.leftAt", DateTime.UtcNow);

        await _calls.UpdateOneAsync(filter, update, cancellationToken: ct);

        var call = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        if (call == null)
            throw new ArgumentException("Call not found");

        // Если все участники отклонили звонок, он завершается автоматически.
        var allDeclinedOrLeft = call.Participants
            .Where(p => p.UserId != call.InitiatorId)
            .All(p => p.Status is "declined" or "left");

        if (allDeclinedOrLeft)
        {
            await _calls.UpdateOneAsync(
                c => c.Id == callId,
                Builders<CallSession>.Update
                    .Set(c => c.Status, call.Participants.Any(p => p.Status == "declined") ? "declined" : "missed")
                    .Set(c => c.EndedAt, DateTime.UtcNow),
                cancellationToken: ct);
        }

        var updated = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        return await MapToResponseAsync(updated!, ct);
    }

    public async Task<CallSessionResponse> EndCallAsync(string callId, Guid userId, CancellationToken ct = default)
    {
        var call = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        if (call == null)
            throw new ArgumentException("Call not found");

        var endedAt = DateTime.UtcNow;
        call.Status = "ended";
        call.EndedAt = endedAt;
        var update = Builders<CallSession>.Update
            .Set(c => c.Status, "ended")
            .Set(c => c.EndedAt, endedAt);

        await _calls.UpdateOneAsync(c => c.Id == callId, update, cancellationToken: ct);

        foreach (var participant in call.Participants.Where(p => p.LeftAt == null))
        {
            participant.LeftAt = endedAt;
            if (participant.Status is not "declined" and not "left")
            {
                participant.Status = "left";
            }
        }

        await _calls.ReplaceOneAsync(c => c.Id == callId, call, cancellationToken: ct);
        await _tracker.ClearCallAsync(callId);

        // Фиксируем в чате системное сообщение о завершении звонка.
        if (!string.IsNullOrEmpty(call.ChatId))
        {
            var duration = call.StartedAt.HasValue
                ? (endedAt - call.StartedAt.Value).TotalSeconds
                : 0;
            var durationStr = TimeSpan.FromSeconds(duration).ToString(@"mm\:ss");
            try
            {
                await _messengerClient.SendSystemMessageAsync(call.ChatId, $"📞 Звонок завершён ({durationStr})", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send call ended message");
            }
        }

        var updated = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        return await MapToResponseAsync(updated!, ct);
    }

    public async Task<CallSessionResponse> LeaveCallAsync(string callId, Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );

        var update = Builders<CallSession>.Update
            .Set("participants.$.status", "left")
            .Set("participants.$.leftAt", DateTime.UtcNow);

        await _calls.UpdateOneAsync(filter, update, cancellationToken: ct);
        await _tracker.RemoveFromCallAsync(callId, userId);

        // Проверяем, сколько участников ещё остаётся в активном звонке.
        var call = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        if (call != null)
        {
            var activeCount = call.Participants.Count(p => p.Status == "joined");
            if (!call.IsGroup && activeCount <= 1)
            {
                // Личный звонок завершаем для обоих, если один участник вышел.
                await EndCallAsync(callId, userId, ct);
            }
            else if (call.IsGroup && activeCount == 0)
            {
                // Групповой звонок завершаем только когда не осталось активных участников.
                await EndCallAsync(callId, userId, ct);
            }
        }

        var final = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        return await MapToResponseAsync(final!, ct);
    }

    public async Task<CallSessionResponse?> GetCallAsync(string callId, CancellationToken ct = default)
    {
        var call = await _calls.Find(c => c.Id == callId).FirstOrDefaultAsync(ct);
        return call != null ? await MapToResponseAsync(call, ct) : null;
    }

    public async Task<CallSessionResponse?> GetActiveCallAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.In(c => c.Status, new[] { "ringing", "active" }),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants,
                p => p.UserId == userId && (p.Status == "joined" || p.Status == "ringing"))
        );

        var call = await _calls.Find(filter).SortByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        return call != null ? await MapToResponseAsync(call, ct) : null;
    }

    public async Task<IReadOnlyList<CallSessionResponse>> GetCallHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.ElemMatch(
            c => c.Participants, p => p.UserId == userId);

        var calls = await _calls.Find(filter)
            .SortByDescending(c => c.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);

        var responses = new List<CallSessionResponse>();
        foreach (var call in calls)
            responses.Add(await MapToResponseAsync(call, ct));

        return responses;
    }

    public async Task ToggleVideoAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );
        await _calls.UpdateOneAsync(filter,
            Builders<CallSession>.Update.Set("participants.$.hasVideo", enabled),
            cancellationToken: ct);
    }

    public async Task ToggleAudioAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );
        await _calls.UpdateOneAsync(filter,
            Builders<CallSession>.Update.Set("participants.$.hasAudio", enabled),
            cancellationToken: ct);
    }

    public async Task ToggleScreenShareAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default)
    {
        var filter = Builders<CallSession>.Filter.And(
            Builders<CallSession>.Filter.Eq(c => c.Id, callId),
            Builders<CallSession>.Filter.ElemMatch(c => c.Participants, p => p.UserId == userId)
        );
        await _calls.UpdateOneAsync(filter,
            Builders<CallSession>.Update.Set("participants.$.isScreenSharing", enabled),
            cancellationToken: ct);
    }

    private async Task<CallSessionResponse> MapToResponseAsync(CallSession call, CancellationToken ct)
    {
        var userIds = call.Participants.Select(p => p.UserId).Distinct().ToList();
        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();

        try
        {
            profiles = await _userLookup.GetUsersByIdsAsync(userIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profiles for call {CallId}", call.Id);
        }

        int? duration = null;
        if (call.StartedAt.HasValue && call.EndedAt.HasValue)
            duration = (int)(call.EndedAt.Value - call.StartedAt.Value).TotalSeconds;

        return new CallSessionResponse
        {
            Id = call.Id,
            ChatId = call.ChatId,
            Type = call.Type,
            Status = call.Status,
            InitiatorId = call.InitiatorId,
            IsGroup = call.IsGroup,
            Participants = call.Participants.Select(p =>
            {
                profiles.TryGetValue(p.UserId, out var profile);
                return new CallParticipantResponse
                {
                    UserId = p.UserId,
                    Status = p.Status,
                    JoinedAt = p.JoinedAt,
                    LeftAt = p.LeftAt,
                    HasVideo = p.HasVideo,
                    HasAudio = p.HasAudio,
                    IsScreenSharing = p.IsScreenSharing,
                    Username = profile?.Username,
                    DisplayName = profile?.DisplayName,
                    AvatarUrl = profile?.AvatarUrl
                };
            }).ToList(),
            StartedAt = call.StartedAt,
            EndedAt = call.EndedAt,
            DurationSeconds = duration,
            CreatedAt = call.CreatedAt
        };
    }
}
