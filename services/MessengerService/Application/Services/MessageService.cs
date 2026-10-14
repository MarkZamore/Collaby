using MongoDB.Bson;
using MongoDB.Driver;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;
using MessengerService.Domain.Entities;
using MessengerService.Infrastructure.Data;

namespace MessengerService.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMongoCollection<Message> _messages;
    private readonly IMongoCollection<Chat> _chats;
    private readonly IUserLookupClient _userLookup;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        MessengerMongoDatabaseAccessor databaseAccessor,
        IUserLookupClient userLookup,
        ILogger<MessageService> logger)
    {
        var database = databaseAccessor.Database;
        _messages = database.GetCollection<Message>("messages");
        _chats = database.GetCollection<Chat>("chats");
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<MessageResponse> SendMessageAsync(string chatId, Guid senderId, SendMessageRequest request, CancellationToken ct = default)
    {
        // Отправлять сообщения могут только участники чата.
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null)
            throw new ArgumentException("Chat not found");
        if (chat.Members.All(m => m.UserId != senderId))
            throw new UnauthorizedAccessException("Not a member of this chat");

        ReplyInfo? replyInfo = null;
        if (!string.IsNullOrEmpty(request.ReplyToMessageId))
        {
            var replyMsg = await _messages.Find(m => m.Id == request.ReplyToMessageId).FirstOrDefaultAsync(ct);
            if (replyMsg != null)
            {
                replyInfo = new ReplyInfo
                {
                    MessageId = replyMsg.Id,
                    SenderId = replyMsg.SenderId,
                    Text = replyMsg.Text?.Length > 100 ? replyMsg.Text[..100] + "..." : replyMsg.Text
                };
            }
        }

        var message = new Message
        {
            ChatId = chatId,
            SenderId = senderId,
            Type = request.Type,
            Text = request.Text,
            ReplyTo = replyInfo,
            File = request.File != null ? new FileAttachment
            {
                FileName = request.File.FileName,
                FileUrl = request.File.FileUrl,
                FileSize = request.File.FileSize,
                ContentType = request.File.ContentType
            } : null
        };

        await _messages.InsertOneAsync(message, cancellationToken: ct);

        // lastMessage в чате поддерживается как денормализованный срез для быстрых списков диалогов.
        var lastMessageUpdate = Builders<Chat>.Update
            .Set(c => c.LastMessage, new LastMessageInfo
            {
                MessageId = message.Id,
                Text = message.Type == "file" ? $"📎 {request.File?.FileName}" : message.Text,
                SenderId = senderId,
                SentAt = message.CreatedAt
            })
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _chats.UpdateOneAsync(c => c.Id == chatId, lastMessageUpdate, cancellationToken: ct);

        return await MapToMessageResponseAsync(message, ct);
    }

    public async Task<IReadOnlyList<MessageResponse>> GetMessagesAsync(string chatId, Guid userId, int limit = 50, string? before = null, CancellationToken ct = default)
    {
        // Историю может читать только участник чата.
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null || chat.Members.All(m => m.UserId != userId))
            return Array.Empty<MessageResponse>();

        var filterBuilder = Builders<Message>.Filter;
        var filter = filterBuilder.Eq(m => m.ChatId, chatId);

        if (!string.IsNullOrEmpty(before))
        {
            filter = filterBuilder.And(filter, filterBuilder.Lt(m => m.Id, before));
        }

        var messages = await _messages.Find(filter)
            .SortByDescending(m => m.CreatedAt)
            .Limit(limit)
            .ToListAsync(ct);

        // Mongo читает последние сообщения в обратном порядке, а API должен вернуть их хронологически.
        messages.Reverse();

        var responses = new List<MessageResponse>();
        // Профили подтягиваются batch-запросом, чтобы не делать отдельный запрос на каждое сообщение.
        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();
        try
        {
            profiles = await _userLookup.GetUsersByIdsAsync(senderIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profiles for messages");
        }

        foreach (var msg in messages)
        {
            profiles.TryGetValue(msg.SenderId, out var profile);
            responses.Add(MapToMessageResponse(msg, profile));
        }

        return responses;
    }

    public async Task<MessageResponse> UpdateMessageAsync(string messageId, Guid userId, UpdateMessageRequest request, CancellationToken ct = default)
    {
        var message = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        if (message == null)
            throw new ArgumentException("Message not found");
        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Can only edit your own messages");

        var update = Builders<Message>.Update
            .Set(m => m.Text, request.Text)
            .Set(m => m.IsEdited, true)
            .Set(m => m.UpdatedAt, DateTime.UtcNow);

        await _messages.UpdateOneAsync(m => m.Id == messageId, update, cancellationToken: ct);

        var updated = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        return await MapToMessageResponseAsync(updated!, ct);
    }

    public async Task DeleteMessageAsync(string messageId, Guid userId, CancellationToken ct = default)
    {
        var message = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        if (message == null)
            throw new ArgumentException("Message not found");
        if (message.SenderId != userId)
            throw new UnauthorizedAccessException("Can only delete your own messages");

        var update = Builders<Message>.Update
            .Set(m => m.IsDeleted, true)
            .Set(m => m.Text, null)
            .Set(m => m.File, null)
            .Set(m => m.UpdatedAt, DateTime.UtcNow);

        await _messages.UpdateOneAsync(m => m.Id == messageId, update, cancellationToken: ct);
    }

    public async Task<MessageResponse> ToggleReactionAsync(string messageId, Guid userId, ToggleReactionRequest request, CancellationToken ct = default)
    {
        var message = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        if (message == null)
            throw new ArgumentException("Message not found");

        var existingReaction = message.Reactions.FirstOrDefault(r => r.Emoji == request.Emoji);

        if (existingReaction != null && existingReaction.UserIds.Contains(userId))
        {
            // Повторное нажатие тем же пользователем снимает его реакцию.
            existingReaction.UserIds.Remove(userId);
            if (existingReaction.UserIds.Count == 0)
                message.Reactions.Remove(existingReaction);
        }
        else if (existingReaction != null)
        {
            existingReaction.UserIds.Add(userId);
        }
        else
        {
            message.Reactions.Add(new Reaction
            {
                Emoji = request.Emoji,
                UserIds = new List<Guid> { userId }
            });
        }

        var update = Builders<Message>.Update.Set(m => m.Reactions, message.Reactions);
        await _messages.UpdateOneAsync(m => m.Id == messageId, update, cancellationToken: ct);

        var updated = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync(ct);
        return await MapToMessageResponseAsync(updated!, ct);
    }

    private async Task<MessageResponse> MapToMessageResponseAsync(Message msg, CancellationToken ct)
    {
        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();
        try
        {
            profiles = await _userLookup.GetUsersByIdsAsync(new[] { msg.SenderId }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch sender profile");
        }

        profiles.TryGetValue(msg.SenderId, out var profile);
        return MapToMessageResponse(msg, profile);
    }

    private static MessageResponse MapToMessageResponse(Message msg, UserLookupResult? profile)
    {
        return new MessageResponse
        {
            Id = msg.Id,
            ChatId = msg.ChatId,
            SenderId = msg.SenderId,
            Type = msg.Type,
            Text = msg.Text,
            File = msg.File != null ? new FileAttachmentResponse
            {
                FileName = msg.File.FileName,
                FileUrl = msg.File.FileUrl,
                FileSize = msg.File.FileSize,
                ContentType = msg.File.ContentType
            } : null,
            ReplyTo = msg.ReplyTo != null ? new ReplyInfoResponse
            {
                MessageId = msg.ReplyTo.MessageId,
                SenderId = msg.ReplyTo.SenderId,
                Text = msg.ReplyTo.Text
            } : null,
            Reactions = msg.Reactions.Select(r => new ReactionResponse
            {
                Emoji = r.Emoji,
                UserIds = r.UserIds,
                Count = r.UserIds.Count
            }).ToList(),
            IsEdited = msg.IsEdited,
            IsDeleted = msg.IsDeleted,
            SenderUsername = profile?.Username,
            SenderDisplayName = profile?.DisplayName,
            SenderAvatarUrl = profile?.AvatarUrl,
            CreatedAt = msg.CreatedAt,
            UpdatedAt = msg.UpdatedAt
        };
    }
}
