using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Requests;

namespace MessengerService.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IChatService _chatService;
    private readonly IPresenceService _presenceService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IMessageService messageService,
        IChatService chatService,
        IPresenceService presenceService,
        ILogger<ChatHub> logger)
    {
        _messageService = messageService;
        _chatService = chatService;
        _presenceService = presenceService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            await _presenceService.SetOnlineAsync(userId);

            // Уведомляем контакты пользователя о его выходе в online.
            var chats = await _chatService.GetUserChatsAsync(userId);
            var contactIds = chats
                .SelectMany(c => c.Members)
                .Select(m => m.UserId)
                .Where(id => id != userId)
                .Distinct();

            foreach (var contactId in contactIds)
            {
                await Clients.Group($"user:{contactId}")
                    .SendAsync("UserOnline", new { userId });
            }

            _logger.LogInformation("User {UserId} connected to ChatHub", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            await _presenceService.SetOfflineAsync(userId);

            var chats = await _chatService.GetUserChatsAsync(userId);
            var contactIds = chats
                .SelectMany(c => c.Members)
                .Select(m => m.UserId)
                .Where(id => id != userId)
                .Distinct();

            foreach (var contactId in contactIds)
            {
                await Clients.Group($"user:{contactId}")
                    .SendAsync("UserOffline", new { userId, lastSeenAt = DateTime.UtcNow });
            }

            _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Подключает пользователя к группе чата для получения realtime-сообщений.
    /// </summary>
    public async Task JoinChat(string chatId)
    {
        var userId = GetUserId();
        var chat = await _chatService.GetChatByIdAsync(chatId, userId);
        if (chat == null) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat:{chatId}");
        _logger.LogInformation("User {UserId} joined chat {ChatId}", userId, chatId);
    }

    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat:{chatId}");
    }

    /// <summary>
    /// Отправляет сообщение через WebSocket.
    /// </summary>
    public async Task SendMessage(string chatId, SendMessageRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var message = await _messageService.SendMessageAsync(chatId, userId, request);

        // Рассылаем сообщение всем участникам группы чата.
        await Clients.Group($"chat:{chatId}").SendAsync("NewMessage", message);

        // Отдельно уведомляем участников чата, которые сейчас не находятся в его группе.
        var chat = await _chatService.GetChatByIdAsync(chatId, userId);
        if (chat != null)
        {
            foreach (var member in chat.Members.Where(m => m.UserId != userId))
            {
                await Clients.Group($"user:{member.UserId}")
                    .SendAsync("ChatUpdated", new
                    {
                        chatId,
                        lastMessage = message,
                        unreadCount = 1
                    });
            }
        }
    }

    /// <summary>
    /// Событие индикатора набора текста.
    /// </summary>
    public async Task StartTyping(string chatId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _presenceService.SetTypingAsync(userId, chatId);
        await Clients.GroupExcept($"chat:{chatId}", Context.ConnectionId)
            .SendAsync("UserTyping", new { chatId, userId });
    }

    public async Task StopTyping(string chatId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _presenceService.ClearTypingAsync(userId, chatId);
        await Clients.GroupExcept($"chat:{chatId}", Context.ConnectionId)
            .SendAsync("UserStoppedTyping", new { chatId, userId });
    }

    /// <summary>
    /// Помечает чат как прочитанный.
    /// </summary>
    public async Task MarkAsRead(string chatId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _chatService.MarkAsReadAsync(chatId, userId);
    }

    /// <summary>
    /// Переключает реакцию на сообщение через WebSocket.
    /// </summary>
    public async Task ToggleReaction(string chatId, string messageId, string emoji)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var message = await _messageService.ToggleReactionAsync(messageId, userId, new ToggleReactionRequest { Emoji = emoji });
        await Clients.Group($"chat:{chatId}").SendAsync("ReactionUpdated", message);
    }

    /// <summary>
    /// Heartbeat поддерживает актуальный online-статус пользователя.
    /// </summary>
    public async Task Heartbeat()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
            await _presenceService.SetOnlineAsync(userId);
    }
}
