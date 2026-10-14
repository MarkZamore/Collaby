using CallService.Application.Interfaces;
using MessengerService.Contracts.Responses;
using MessengerService.Domain.Entities;
using MessengerService.Hubs;
using MessengerService.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace RealtimeHost;

internal sealed class InProcessMessengerClient : IMessengerClient
{
    private readonly MessengerMongoDatabaseAccessor _databaseAccessor;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<InProcessMessengerClient> _logger;

    public InProcessMessengerClient(
        MessengerMongoDatabaseAccessor databaseAccessor,
        IHubContext<ChatHub> hubContext,
        ILogger<InProcessMessengerClient> logger)
    {
        _databaseAccessor = databaseAccessor;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendSystemMessageAsync(string chatId, string text, CancellationToken ct = default)
    {
        try
        {
            var database = _databaseAccessor.Database;
            var chats = database.GetCollection<Chat>("chats");
            var messages = database.GetCollection<Message>("messages");

            var chat = await chats.Find(entry => entry.Id == chatId).FirstOrDefaultAsync(ct);
            if (chat is null)
            {
                _logger.LogWarning("Cannot publish system message because chat {ChatId} was not found.", chatId);
                return;
            }

            var message = new Message
            {
                ChatId = chatId,
                SenderId = Guid.Empty,
                Type = "system",
                Text = text,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await messages.InsertOneAsync(message, cancellationToken: ct);

            var lastMessage = new LastMessageInfo
            {
                MessageId = message.Id,
                Text = message.Text,
                SenderId = message.SenderId,
                SentAt = message.CreatedAt
            };

            await chats.UpdateOneAsync(
                entry => entry.Id == chatId,
                Builders<Chat>.Update
                    .Set(entry => entry.LastMessage, lastMessage)
                    .Set(entry => entry.UpdatedAt, DateTime.UtcNow),
                cancellationToken: ct);

            var payload = new MessageResponse
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Type = message.Type,
                Text = message.Text,
                IsEdited = false,
                IsDeleted = false,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt
            };

            await _hubContext.Clients.Group($"chat:{chatId}").SendAsync("NewMessage", payload, ct);

            var sidebarPayload = new
            {
                chatId,
                lastMessage = new
                {
                    messageId = payload.Id,
                    text = payload.Text,
                    senderId = payload.SenderId,
                    sentAt = payload.CreatedAt
                },
                unreadCount = 1
            };

            foreach (var member in chat.Members)
            {
                await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("NewMessage", payload, ct);
                await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("ChatUpdated", sidebarPayload, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish in-process system message for chat {ChatId}.", chatId);
        }
    }
}
