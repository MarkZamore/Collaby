using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Enums;

namespace ManagementService.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IRealtimeNotifier _notifier;

    public NotificationsController(
        INotificationService notificationService,
        IWorkspaceService workspaceService,
        IRealtimeNotifier notifier)
    {
        _notificationService = notificationService;
        _workspaceService = workspaceService;
        _notifier = notifier;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub")
                 ?? User.FindFirst("userId");
        if (claim == null || !Guid.TryParse(claim.Value, out var uid))
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        return uid;
    }

    /// <summary>Получить уведомления текущего пользователя</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var userId = GetCurrentUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, limit, offset);
        return Ok(notifications);
    }

    /// <summary>Количество непрочитанных</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    /// <summary>Пометить одно уведомление прочитанным</summary>
    [HttpPut("{id}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = GetCurrentUserId();
        var ok = await _notificationService.MarkAsReadAsync(id, userId);
        return ok ? NoContent() : NotFound();
    }

    /// <summary>Пометить все уведомления прочитанными</summary>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }

    /// <summary>Принять/отклонить инвайт через уведомление</summary>
    [HttpPut("{id}/action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActionStatus(Guid id, [FromBody] ActionStatusRequest request)
    {
        var userId = GetCurrentUserId();

        // Get notification to find type and referenceId
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, 200, 0);
        var notification = notifications.FirstOrDefault(n => n.Id == id);
        if (notification == null) return NotFound();

        // If already actioned, reject
        if (!string.IsNullOrEmpty(notification.ActionStatus))
            return BadRequest(new { error = "Already actioned" });

        // Process workspace invite accept/decline
        if (notification.Type == NotificationType.WorkspaceInvite.ToString() && notification.ReferenceId.HasValue)
        {
            var workspaceId = notification.ReferenceId.Value;

            if (request.Status == "accepted")
            {
                var accepted = await _workspaceService.AcceptInviteAsync(workspaceId, userId);
                if (!accepted)
                    return BadRequest(new { error = "Invitation not found or already processed" });

                // Broadcast member change to workspace so other members see the update
                _ = _notifier.MemberChanged(workspaceId);
            }
            else if (request.Status == "declined")
            {
                await _workspaceService.DeclineInviteAsync(workspaceId, userId);
            }
        }

        // Update notification status
        var ok = await _notificationService.SetActionStatusAsync(id, userId, request.Status);
        return ok ? NoContent() : NotFound();
    }
}

public class ActionStatusRequest
{
    public string Status { get; set; } = string.Empty; // "accepted" | "declined"
}
