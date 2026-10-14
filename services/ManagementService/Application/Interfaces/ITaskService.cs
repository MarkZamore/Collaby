using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

public interface ITaskService
{
    /// <summary>Задачи «к выполнению», назначенные пользователю, по всем его воркспейсам (с контекстом workspace/project).</summary>
    System.Threading.Tasks.Task<IEnumerable<InboxItemResponse>> GetInboxAsync(Guid userId, CancellationToken ct = default);

    // ─── Task CRUD ──────────────────────────────────────────────────────────
    System.Threading.Tasks.Task<TaskItem> CreateTaskAsync(Guid projectId, string title, string? description = null,
        Guid? assigneeId = null, TaskPriority priority = TaskPriority.Medium, DateTime? dueDate = null,
        Dictionary<string, string>? stickerValues = null, Guid userId = default);

    System.Threading.Tasks.Task<IEnumerable<TaskItem>> GetProjectTasksAsync(Guid projectId, Guid? statusId = null,
        Guid? assigneeId = null, TaskPriority? priority = null, Guid userId = default);

    System.Threading.Tasks.Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid userId);

    System.Threading.Tasks.Task<TaskItem?> UpdateTaskAsync(Guid taskId, string title, string? description = null,
        TaskPriority? priority = null, DateTime? dueDate = null, Dictionary<string, string>? stickerValues = null,
        Guid userId = default);

    System.Threading.Tasks.Task<bool> DeleteTaskAsync(Guid taskId, Guid userId);
    System.Threading.Tasks.Task<bool> MoveTaskAsync(Guid taskId, Guid newStatusId, int newOrder, Guid userId);
    System.Threading.Tasks.Task<bool> AssignTaskAsync(Guid taskId, Guid? assigneeId, Guid userId);

    // ─── Task Stickers ──────────────────────────────────────────────────────
    System.Threading.Tasks.Task<TaskItem?> UpdateTaskStickersAsync(Guid taskId, Dictionary<string, string> stickerValues, Guid userId);

    // ─── SubTasks ───────────────────────────────────────────────────────────
    System.Threading.Tasks.Task<SubTaskItem?> GetSubTaskByIdAsync(Guid subtaskId, Guid userId);
    System.Threading.Tasks.Task<SubTaskItem> CreateSubTaskAsync(Guid taskId, string title, Guid userId);
    System.Threading.Tasks.Task<SubTaskItem?> UpdateSubTaskAsync(Guid subtaskId, string? title, bool? isCompleted, Guid userId);
    System.Threading.Tasks.Task<bool> DeleteSubTaskAsync(Guid subtaskId, Guid userId);

    // ─── Comments ───────────────────────────────────────────────────────────
    System.Threading.Tasks.Task<TaskComment?> GetCommentByIdAsync(Guid commentId, Guid userId);
    System.Threading.Tasks.Task<TaskComment> AddCommentAsync(Guid taskId, string text, Guid userId);
    System.Threading.Tasks.Task<TaskComment?> UpdateCommentAsync(Guid commentId, string text, Guid userId);
    System.Threading.Tasks.Task<bool> DeleteCommentAsync(Guid commentId, Guid userId);

    // ─── Labels ─────────────────────────────────────────────────────────────
    System.Threading.Tasks.Task<ProjectLabel> CreateProjectLabelAsync(Guid projectId, string name, string color, Guid userId);
    System.Threading.Tasks.Task<IEnumerable<ProjectLabel>> GetProjectLabelsAsync(Guid projectId, Guid userId);
    System.Threading.Tasks.Task<ProjectLabel?> UpdateProjectLabelAsync(Guid labelId, string? name, string? color, Guid userId);
    System.Threading.Tasks.Task<bool> DeleteProjectLabelAsync(Guid labelId, Guid userId);
    System.Threading.Tasks.Task<bool> AssignLabelToTaskAsync(Guid taskId, Guid labelId, Guid userId);
    System.Threading.Tasks.Task<bool> RemoveLabelFromTaskAsync(Guid taskId, Guid labelId, Guid userId);
}