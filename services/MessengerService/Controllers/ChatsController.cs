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
[Route("chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatsController(IChatService chatService, IHubContext<ChatHub> hubContext)
    {
        _chatService = chatService;
        _hubContext = hubContext;
    }

    [HttpPost("private")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatResponse>> CreatePrivateChat([FromBody] CreatePrivateChatRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatService.CreatePrivateChatAsync(userId, request, ct);
        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, chat);
    }

    [HttpPost("project")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatResponse>> CreateProjectChat([FromBody] CreateProjectChatRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatService.CreateProjectChatAsync(userId, request, ct);
        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, chat);
    }

    [HttpPost("channel")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatResponse>> CreateChannel([FromBody] CreateChannelRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatService.CreateChannelAsync(userId, request, ct);
        return CreatedAtAction(nameof(GetChat), new { chatId = chat.Id }, chat);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ChatResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatResponse>>> GetUserChats(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chats = await _chatService.GetUserChatsAsync(userId, ct);
        return Ok(chats);
    }

    [HttpGet("{chatId}")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> GetChat(string chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatService.GetChatByIdAsync(chatId, userId, ct);
        if (chat == null) return NotFound();
        return Ok(chat);
    }

    [HttpDelete("{chatId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChat(string chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.DeleteChatAsync(chatId, userId, ct);

        return result.Status switch
        {
            DeleteChatStatus.Deleted => NoContent(),
            DeleteChatStatus.NotFound => NotFound(),
            DeleteChatStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPost("{chatId}/clear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClearChat(string chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _chatService.ClearChatAsync(chatId, userId, ct);

        if (result.Status == ClearChatStatus.Cleared)
        {
            var chat = await _chatService.GetChatByIdAsync(chatId, userId, ct);
            if (chat != null)
            {
                foreach (var member in chat.Members)
                {
                    await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("ChatUpdated", new
                    {
                        chatId,
                        lastMessage = (object?)null,
                        unreadCount = 0,
                    }, ct);
                    await _hubContext.Clients.Group($"user:{member.UserId}").SendAsync("ChatCleared", new
                    {
                        chatId,
                    }, ct);
                }
            }

            return NoContent();
        }

        return result.Status switch
        {
            ClearChatStatus.NotFound => NotFound(),
            ClearChatStatus.Forbidden => Forbid(),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{chatId}")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> UpdateChat(string chatId, [FromBody] UpdateChatRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var chat = await _chatService.UpdateChatAsync(chatId, userId, request, ct);
        return Ok(chat);
    }

    [HttpPost("{chatId}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMembers(string chatId, [FromBody] AddChatMembersRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _chatService.AddMembersAsync(chatId, userId, request, ct);
        return NoContent();
    }

    [HttpDelete("{chatId}/members/{memberUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(string chatId, Guid memberUserId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _chatService.RemoveMemberAsync(chatId, userId, memberUserId, ct);
        return NoContent();
    }

    [HttpPost("{chatId}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LeaveChat(string chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _chatService.LeaveChatAsync(chatId, userId, ct);
        return NoContent();
    }

    [HttpPost("{chatId}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAsRead(string chatId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _chatService.MarkAsReadAsync(chatId, userId, ct);
        return NoContent();
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
