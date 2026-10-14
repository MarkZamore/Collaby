using Microsoft.AspNetCore.SignalR;
using ManagementService.Hubs;

namespace ManagementService.Application.Services;

/// <summary>
/// Сервис для отправки real-time уведомлений через SignalR.
/// Контроллеры и сервисы используют его для push-событий.
/// </summary>
public interface IRealtimeNotifier
{
    Task TaskCreated(Guid projectId, object taskResponse);
    Task TaskUpdated(Guid projectId, object taskResponse);
    Task TaskDeleted(Guid projectId, Guid taskId);
    Task TaskMoved(Guid projectId, Guid taskId, Guid statusId, int order);
    Task SubTaskChanged(Guid projectId, Guid taskId);
    Task CommentChanged(Guid projectId, Guid taskId);
    Task LabelChanged(Guid projectId);
    Task ProjectChanged(Guid workspaceId);
    Task StatusChanged(Guid projectId);
    Task WorkspaceChanged(Guid workspaceId);
    Task MemberChanged(Guid workspaceId);
}

public class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<WorkspaceHub> _hub;

    public RealtimeNotifier(IHubContext<WorkspaceHub> hub)
    {
        _hub = hub;
    }

    public Task TaskCreated(Guid projectId, object taskResponse)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("TaskCreated", taskResponse);

    public Task TaskUpdated(Guid projectId, object taskResponse)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("TaskUpdated", taskResponse);

    public Task TaskDeleted(Guid projectId, Guid taskId)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("TaskDeleted", new { taskId });

    public Task TaskMoved(Guid projectId, Guid taskId, Guid statusId, int order)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("TaskMoved", new { taskId, statusId, order });

    public Task SubTaskChanged(Guid projectId, Guid taskId)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("SubTaskChanged", new { taskId });

    public Task CommentChanged(Guid projectId, Guid taskId)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("CommentChanged", new { taskId });

    public Task LabelChanged(Guid projectId)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("LabelChanged", new { projectId });

    public Task ProjectChanged(Guid workspaceId)
        => _hub.Clients.Group($"workspace:{workspaceId}").SendAsync("ProjectChanged", new { workspaceId });

    public Task StatusChanged(Guid projectId)
        => _hub.Clients.Group($"project:{projectId}").SendAsync("StatusChanged", new { projectId });

    public Task WorkspaceChanged(Guid workspaceId)
        => _hub.Clients.Group($"workspace:{workspaceId}").SendAsync("WorkspaceChanged", new { workspaceId });

    public Task MemberChanged(Guid workspaceId)
        => _hub.Clients.Group($"workspace:{workspaceId}").SendAsync("MemberChanged", new { workspaceId });
}
