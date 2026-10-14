using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ManagementService.Hubs;

/// <summary>
/// SignalR хаб для real-time обновлений в воркспейсе.
/// Клиенты присоединяются к группе "workspace:{workspaceId}" и
/// получают push-события при изменениях задач, участников и т.д.
/// </summary>
[Authorize]
public class WorkspaceHub : Hub
{
    private readonly ILogger<WorkspaceHub> _logger;

    public WorkspaceHub(ILogger<WorkspaceHub> logger)
    {
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Клиент вызывает этот метод, чтобы подписаться на обновления воркспейса.
    /// </summary>
    public async Task JoinWorkspace(string workspaceId)
    {
        var userId = GetUserId();
        var groupName = $"workspace:{workspaceId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} joined SignalR group {Group}", userId, groupName);
    }

    /// <summary>
    /// Клиент отписывается от обновлений воркспейса.
    /// </summary>
    public async Task LeaveWorkspace(string workspaceId)
    {
        var userId = GetUserId();
        var groupName = $"workspace:{workspaceId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("User {UserId} left SignalR group {Group}", userId, groupName);
    }

    /// <summary>
    /// Клиент подписывается на обновления конкретного проекта
    /// (более гранулярно, чем весь workspace).
    /// </summary>
    public async Task JoinProject(string projectId)
    {
        var groupName = $"project:{projectId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveProject(string projectId)
    {
        var groupName = $"project:{projectId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            var userGroup = $"user:{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);
            _logger.LogInformation("User {UserId} auto-joined personal group {Group}", userId, userGroup);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
