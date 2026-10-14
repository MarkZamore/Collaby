using MongoDB.Driver;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;
using MessengerService.Domain.Entities;
using MessengerService.Infrastructure.Data;

namespace MessengerService.Application.Services;

public class ChatService : IChatService
{
    private readonly IMongoCollection<Chat> _chats;
    private readonly IMongoCollection<Message> _messages;
    private readonly IChatDeletionStore _chatDeletionStore;
    private readonly IUserLookupClient _userLookup;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        MessengerMongoDatabaseAccessor databaseAccessor,
        IUserLookupClient userLookup,
        ILogger<ChatService> logger)
    {
        var database = databaseAccessor.Database;
        _chats = database.GetCollection<Chat>("chats");
        _messages = database.GetCollection<Message>("messages");
        _chatDeletionStore = new MongoChatDeletionStore(_chats, _messages);
        _userLookup = userLookup;
        _logger = logger;
    }

    internal ChatService(
        IUserLookupClient userLookup,
        ILogger<ChatService> logger,
        IChatDeletionStore chatDeletionStore)
    {
        _chats = null!;
        _messages = null!;
        _chatDeletionStore = chatDeletionStore;
        _userLookup = userLookup;
        _logger = logger;
    }

    public async Task<ChatResponse> CreatePrivateChatAsync(Guid userId, CreatePrivateChatRequest request, CancellationToken ct = default)
    {
        // Для private-чата сначала проверяем, не существует ли уже диалог между этими пользователями.
        var filter = Builders<Chat>.Filter.And(
            Builders<Chat>.Filter.Eq(c => c.Type, "private"),
            Builders<Chat>.Filter.ElemMatch(c => c.Members, m => m.UserId == userId),
            Builders<Chat>.Filter.ElemMatch(c => c.Members, m => m.UserId == request.ParticipantId)
        );

        var existing = await _chats.Find(filter).FirstOrDefaultAsync(ct);
        if (existing != null)
            return await MapToChatResponseAsync(existing, userId, ct);

        var chat = new Chat
        {
            Type = "private",
            CreatedBy = userId,
            Members = new List<ChatMember>
            {
                new() { UserId = userId, Role = "member" },
                new() { UserId = request.ParticipantId, Role = "member" }
            }
        };

        await _chats.InsertOneAsync(chat, cancellationToken: ct);
        return await MapToChatResponseAsync(chat, userId, ct);
    }

    public async Task<ChatResponse> CreateProjectChatAsync(Guid userId, CreateProjectChatRequest request, CancellationToken ct = default)
    {
        var members = new List<ChatMember>
        {
            new() { UserId = userId, Role = "admin" }
        };

        if (request.MemberIds != null)
        {
            foreach (var memberId in request.MemberIds.Where(id => id != userId))
                members.Add(new ChatMember { UserId = memberId, Role = "member" });
        }

        var chat = new Chat
        {
            Type = "project",
            Name = request.Name,
            Description = request.Description,
            ProjectId = request.ProjectId,
            WorkspaceId = request.WorkspaceId,
            CreatedBy = userId,
            Members = members
        };

        await _chats.InsertOneAsync(chat, cancellationToken: ct);
        return await MapToChatResponseAsync(chat, userId, ct);
    }

    public async Task<ChatResponse> CreateChannelAsync(Guid userId, CreateChannelRequest request, CancellationToken ct = default)
    {
        var members = new List<ChatMember>
        {
            new() { UserId = userId, Role = "admin" }
        };

        if (request.MemberIds != null)
        {
            foreach (var memberId in request.MemberIds.Where(id => id != userId))
                members.Add(new ChatMember { UserId = memberId, Role = "member" });
        }

        var chat = new Chat
        {
            Type = "channel",
            Name = request.Name,
            Description = request.Description,
            WorkspaceId = request.WorkspaceId,
            CreatedBy = userId,
            Members = members
        };

        await _chats.InsertOneAsync(chat, cancellationToken: ct);
        return await MapToChatResponseAsync(chat, userId, ct);
    }

    public async Task<IReadOnlyList<ChatResponse>> GetUserChatsAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<Chat>.Filter.ElemMatch(c => c.Members, m => m.UserId == userId);
        var chats = await _chats.Find(filter)
            .SortByDescending(c => c.UpdatedAt)
            .ToListAsync(ct);

        var responses = new List<ChatResponse>();
        foreach (var chat in chats)
            responses.Add(await MapToChatResponseAsync(chat, userId, ct));

        return responses;
    }

    public async Task<ChatResponse?> GetChatByIdAsync(string chatId, Guid userId, CancellationToken ct = default)
    {
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null || chat.Members.All(m => m.UserId != userId))
            return null;

        return await MapToChatResponseAsync(chat, userId, ct);
    }

    public async Task<DeleteChatResult> DeleteChatAsync(string chatId, Guid userId, CancellationToken ct = default)
    {
        var chat = await _chatDeletionStore.GetChatAsync(chatId, ct);
        if (chat == null)
            return DeleteChatResult.NotFound;

        var member = chat.Members.FirstOrDefault(item => item.UserId == userId);
        if (member == null)
            return DeleteChatResult.Forbidden;

        var isPrivateChat = string.Equals(chat.Type, "private", StringComparison.OrdinalIgnoreCase);
        var isAdmin = string.Equals(member.Role, "admin", StringComparison.OrdinalIgnoreCase);
        var isCreator = chat.CreatedBy == userId;

        if ((isPrivateChat && !isCreator) || (!isPrivateChat && !isAdmin))
            return DeleteChatResult.Forbidden;

        await _chatDeletionStore.DeleteMessagesAsync(chatId, ct);
        await _chatDeletionStore.DeleteChatAsync(chatId, ct);

        return DeleteChatResult.Deleted;
    }

    public async Task<ClearChatResult> ClearChatAsync(string chatId, Guid userId, CancellationToken ct = default)
    {
        var chat = await _chatDeletionStore.GetChatAsync(chatId, ct);
        if (chat == null)
            return ClearChatResult.NotFound;

        var member = chat.Members.FirstOrDefault(item => item.UserId == userId);
        if (member == null)
            return ClearChatResult.Forbidden;

        var isPrivateChat = string.Equals(chat.Type, "private", StringComparison.OrdinalIgnoreCase);
        var isAdmin = string.Equals(member.Role, "admin", StringComparison.OrdinalIgnoreCase);
        if (!isPrivateChat && !isAdmin)
            return ClearChatResult.Forbidden;

        await _chatDeletionStore.DeleteMessagesAsync(chatId, ct);

        var update = Builders<Chat>.Update
            .Set(c => c.LastMessage, null)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _chats.UpdateOneAsync(c => c.Id == chatId, update, cancellationToken: ct);

        return ClearChatResult.Cleared;
    }

    public async Task<ChatResponse> UpdateChatAsync(string chatId, Guid userId, UpdateChatRequest request, CancellationToken ct = default)
    {
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null)
            throw new ArgumentException("Chat not found");

        var member = chat.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            throw new UnauthorizedAccessException("Not a member of this chat");
        if (chat.Type != "private" && member.Role != "admin")
            throw new UnauthorizedAccessException("Only admins can update chat settings");

        var update = Builders<Chat>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow);
        if (request.Name != null)
            update = update.Set(c => c.Name, request.Name);
        if (request.Description != null)
            update = update.Set(c => c.Description, request.Description);

        await _chats.UpdateOneAsync(c => c.Id == chatId, update, cancellationToken: ct);

        var updated = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        return await MapToChatResponseAsync(updated!, userId, ct);
    }

    public async Task AddMembersAsync(string chatId, Guid userId, AddChatMembersRequest request, CancellationToken ct = default)
    {
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null)
            throw new ArgumentException("Chat not found");

        var member = chat.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            throw new UnauthorizedAccessException("Not a member of this chat");

        var existingIds = chat.Members.Select(m => m.UserId).ToHashSet();
        var newMembers = request.UserIds
            .Where(id => !existingIds.Contains(id))
            .Select(id => new ChatMember { UserId = id, Role = "member" })
            .ToList();

        if (newMembers.Count > 0)
        {
            var update = Builders<Chat>.Update
                .PushEach(c => c.Members, newMembers)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);
            await _chats.UpdateOneAsync(c => c.Id == chatId, update, cancellationToken: ct);
        }
    }

    public async Task RemoveMemberAsync(string chatId, Guid userId, Guid memberUserId, CancellationToken ct = default)
    {
        var chat = await _chats.Find(c => c.Id == chatId).FirstOrDefaultAsync(ct);
        if (chat == null)
            throw new ArgumentException("Chat not found");

        var actor = chat.Members.FirstOrDefault(m => m.UserId == userId);
        if (actor == null || actor.Role != "admin")
            throw new UnauthorizedAccessException("Only admins can remove members");

        var update = Builders<Chat>.Update
            .PullFilter(c => c.Members, m => m.UserId == memberUserId)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _chats.UpdateOneAsync(c => c.Id == chatId, update, cancellationToken: ct);
    }

    public async Task LeaveChatAsync(string chatId, Guid userId, CancellationToken ct = default)
    {
        var update = Builders<Chat>.Update
            .PullFilter(c => c.Members, m => m.UserId == userId)
            .Set(c => c.UpdatedAt, DateTime.UtcNow);
        await _chats.UpdateOneAsync(c => c.Id == chatId, update, cancellationToken: ct);
    }

    public async Task MarkAsReadAsync(string chatId, Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<Chat>.Filter.And(
            Builders<Chat>.Filter.Eq(c => c.Id, chatId),
            Builders<Chat>.Filter.ElemMatch(c => c.Members, m => m.UserId == userId)
        );
        var update = Builders<Chat>.Update.Set("members.$.lastReadAt", DateTime.UtcNow);
        await _chats.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    private async Task<ChatResponse> MapToChatResponseAsync(Chat chat, Guid currentUserId, CancellationToken ct)
    {
        var userIds = chat.Members.Select(m => m.UserId).Distinct().ToList();
        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();

        try
        {
            profiles = await _userLookup.GetUsersByIdsAsync(userIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch user profiles for chat {ChatId}", chat.Id);
        }

        // Непрочитанное количество считаем отдельно для каждого участника.
        var member = chat.Members.FirstOrDefault(m => m.UserId == currentUserId);
        int unreadCount = 0;
        if (member?.LastReadAt != null)
        {
            unreadCount = (int)await _messages.CountDocumentsAsync(
                Builders<Message>.Filter.And(
                    Builders<Message>.Filter.Eq(m => m.ChatId, chat.Id),
                    Builders<Message>.Filter.Gt(m => m.CreatedAt, member.LastReadAt.Value),
                    Builders<Message>.Filter.Ne(m => m.SenderId, currentUserId),
                    Builders<Message>.Filter.Eq(m => m.IsDeleted, false)
                ), cancellationToken: ct);
        }

        return new ChatResponse
        {
            Id = chat.Id,
            Type = chat.Type,
            Name = chat.Name,
            Description = chat.Description,
            ProjectId = chat.ProjectId,
            WorkspaceId = chat.WorkspaceId,
            CreatedBy = chat.CreatedBy,
            UnreadCount = unreadCount,
            Members = chat.Members.Select(m =>
            {
                profiles.TryGetValue(m.UserId, out var profile);
                return new Contracts.Responses.ChatMemberResponse
                {
                    UserId = m.UserId,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt,
                    Username = profile?.Username,
                    DisplayName = profile?.DisplayName,
                    AvatarUrl = profile?.AvatarUrl
                };
            }).ToList(),
            LastMessage = chat.LastMessage != null ? new LastMessageResponse
            {
                MessageId = chat.LastMessage.MessageId,
                Text = chat.LastMessage.Text,
                SenderId = chat.LastMessage.SenderId,
                SentAt = chat.LastMessage.SentAt
            } : null,
            CreatedAt = chat.CreatedAt,
            UpdatedAt = chat.UpdatedAt
        };
    }
}
