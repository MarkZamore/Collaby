using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

public interface IGamificationHook
{
    Task OnTaskCompleted(Guid workspaceId, Guid taskId, Guid userId, Guid projectId, TaskPriority priority);
    Task OnTaskReopened(Guid workspaceId, Guid taskId, Guid userId, Guid projectId);
    Task OnSubtaskCompleted(Guid workspaceId, Guid subtaskId, Guid userId, Guid projectId);
    Task OnCommentAdded(Guid workspaceId, Guid taskId, Guid userId, Guid projectId);
}
