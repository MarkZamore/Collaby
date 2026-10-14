using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;
using MessengerService.Hubs;

namespace MessengerService.Controllers;

[ApiController]
[Route("chats/{chatId}/messages")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public MessagesController(IMessageService messageService, IChatService chatService, IHubContext<ChatHub> hubContext)
    {
        _messageService = messageService;
        _chatService = chatService;
        _hubContext = hubContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<MessageResponse>> SendMessage(string chatId, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var message = await _messageService.SendMessageAsync(chatId, userId, request, ct);

        // Рассылаем NewMessage всем участникам группы чата для realtime-доставки.
        await _hubContext.Clients.Group($"chat:{chatId}").SendAsync("NewMessage", message, ct);

        // Дополнительно уведомляем каждого участника через его персональную группу для обновления sidebar.
        var chat = await _chatService.GetChatByIdAsync(chatId, userId, ct);
        if (chat != null)
        {
            var lastMsg = new { messageId = message.Id, text = message.Text, senderId = message.SenderId, sentAt = message.CreatedAt };
            foreach (var member in chat.Members.Where(m => m.UserId != userId))
            {
                await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("NewMessage", message, ct);
                await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("ChatUpdated", new
                {
                    chatId,
                    lastMessage = lastMsg,
                    unreadCount = 1
                }, ct);
            }
        }

        return Created($"{Request.Path}/{message.Id}", message);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MessageResponse>>> GetMessages(
        string chatId,
        [FromQuery] int limit = 50,
        [FromQuery] string? before = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var messages = await _messageService.GetMessagesAsync(chatId, userId, limit, before, ct);
        return Ok(messages);
    }

    [HttpPut("{messageId}")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> UpdateMessage(string chatId, string messageId, [FromBody] UpdateMessageRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var message = await _messageService.UpdateMessageAsync(messageId, userId, request, ct);
        return Ok(message);
    }

    [HttpDelete("{messageId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMessage(string chatId, string messageId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _messageService.DeleteMessageAsync(messageId, userId, ct);
        return NoContent();
    }

    [HttpPost("{messageId}/reactions")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageResponse>> ToggleReaction(string chatId, string messageId, [FromBody] ToggleReactionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var message = await _messageService.ToggleReactionAsync(messageId, userId, request, ct);
        return Ok(message);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        return userId;
    }
}
