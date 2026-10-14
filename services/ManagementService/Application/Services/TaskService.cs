using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public class TaskService : ITaskService
{
    private static readonly EventId GamificationHookFailed = new(7001, nameof(GamificationHookFailed));

    private readonly ManagementDbContext _context;
    private readonly IWorkspaceService _workspaceService;
    private readonly IGamificationHook? _gamificationHook;
    private readonly IProjectProgressSnapshotOutbox _projectProgressSnapshotOutbox;
    private readonly ILogger<TaskService> _logger;

    public TaskService(
        ManagementDbContext context,
        IWorkspaceService workspaceService,
        IEnumerable<IGamificationHook> gamificationHooks,
        IProjectProgressSnapshotOutbox projectProgressSnapshotOutbox,
        ILogger<TaskService> logger)
    {
        _context = context;
        _workspaceService = workspaceService;
        _gamificationHook = gamificationHooks.SingleOrDefault();
        _projectProgressSnapshotOutbox = projectProgressSnapshotOutbox;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<IEnumerable<InboxItemResponse>> GetInboxAsync(Guid userId, CancellationToken ct = default)
    {
        var workspaceIds = await _context.WorkspaceMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.WorkspaceId)
            .ToListAsync(ct);

        if (workspaceIds.Count == 0)
            return Array.Empty<InboxItemResponse>();

        var projects = await _context.Projects
            .Where(p => workspaceIds.Contains(p.WorkspaceId))
            .Include(p => p.Workspace)
            .Include(p => p.Statuses.OrderBy(s => s.Order))
            .AsNoTracking()
            .ToListAsync(ct);

        var projectInfo = new Dictionary<Guid, (Guid WorkspaceId, string WorkspaceName, string ProjectName, Guid FirstStatusId)>();
        foreach (var p in projects)
        {
            var firstStatus = p.Statuses?.FirstOrDefault();
            if (firstStatus == null || p.Workspace == null) continue;
            projectInfo[p.Id] = (p.WorkspaceId, p.Workspace.Name, p.Name, firstStatus.Id);
        }

        var projectIds = projectInfo.Keys.ToList();

        var allTasks = await _context.Tasks
            .Where(t => t.AssigneeId == userId && !t.IsArchived && projectIds.Contains(t.ProjectId) && t.StatusId != null)
            .Include(t => t.Status)
            .Include(t => t.Project).ThenInclude(p => p!.Workspace)
            .Include(t => t.SubTasks.OrderBy(s => s.Order))
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
            .Include(t => t.LabelAssignments).ThenInclude(la => la.Label)
            .AsNoTracking()
            .ToListAsync(ct);

        var tasks = allTasks.Where(t => projectInfo.TryGetValue(t.ProjectId, out var info) && info.FirstStatusId == t.StatusId)
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue).ThenBy(t => t.CreatedAt)
            .ToList();

        return tasks.Select(t =>
        {
            var info = projectInfo[t.ProjectId];
            return new InboxItemResponse
            {
                Id = t.Id,
                ProjectId = t.ProjectId,
                WorkspaceId = info.WorkspaceId,
                WorkspaceName = info.WorkspaceName,
                ProjectName = info.ProjectName,
                StatusId = t.StatusId ?? Guid.Empty,
                StatusName = t.Status?.Name ?? "Unknown",
                Title = t.Title,
                Description = t.Description,
                CreatedBy = t.CreatedBy,
                AssigneeId = t.AssigneeId,
                Order = t.Order,
                Priority = t.Priority,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                DueDate = t.DueDate,
                IsArchived = t.IsArchived,
                SubTasks = (t.SubTasks ?? new List<SubTaskItem>()).Select(s => new SubTaskResponse
                {
                    Id = s.Id,
                    Title = s.Title,
                    IsCompleted = s.IsCompleted,
                    Order = s.Order,
                    CreatedAt = s.CreatedAt,
                }).ToList(),
                Comments = (t.Comments ?? new List<TaskComment>()).Select(c => new CommentResponse
                {
                    Id = c.Id,
                    AuthorId = c.AuthorId,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                }).ToList(),
                Labels = (t.LabelAssignments ?? new List<TaskLabelAssignment>())
                    .Where(la => la.Label != null)
                    .Select(la => new LabelResponse
                    {
                        Id = la.Label!.Id,
                        Name = la.Label.Name,
                        Color = la.Label.Color,
                    }).ToList(),
            };
        }).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Операции CRUD для задач.
    // ═══════════════════════════════════════════════════════════════════════

    public async System.Threading.Tasks.Task<TaskItem> CreateTaskAsync(Guid projectId, string title, string? description = null,
        Guid? assigneeId = null, TaskPriority priority = TaskPriority.Medium, DateTime? dueDate = null,
        Dictionary<string, string>? stickerValues = null, Guid userId = default)
    {
        var project = await _context.Projects.Include(p => p.Workspace).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new ArgumentException("Project not found");
        await _workspaceService.RequireTaskMutationPermissionAsync(project.WorkspaceId, userId);

        var defaultStatus = await _context.ProjectStatuses
            .Where(s => s.ProjectId == projectId).OrderBy(s => s.Order).FirstOrDefaultAsync();
        if (defaultStatus == null) throw new InvalidOperationException("No statuses found for project");

        var maxOrder = await _context.Tasks.Where(t => t.StatusId == defaultStatus.Id).MaxAsync(t => (int?)t.Order) ?? 0;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            StatusId = defaultStatus.Id,
            Title = title.Trim(),
            Description = description?.Trim(),
            AssigneeId = assigneeId,
            CreatedBy = userId,
            Priority = priority,
            DueDate = dueDate,
            StickerValues = stickerValues != null ? JsonSerializer.Serialize(stickerValues) : null,
            Order = maxOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(projectId);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Task created: {TaskId} in project {ProjectId}", task.Id, projectId);
        return await GetTaskByIdAsync(task.Id, userId) ?? task;
    }

    public async System.Threading.Tasks.Task<IEnumerable<TaskItem>> GetProjectTasksAsync(Guid projectId, Guid? statusId = null,
        Guid? assigneeId = null, TaskPriority? priority = null, Guid userId = default)
    {
        var project = await _context.Projects.Include(p => p.Workspace).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new ArgumentException("Project not found");
        if (!await _workspaceService.HasAccessToWorkspaceAsync(project.WorkspaceId, userId))
            throw new UnauthorizedAccessException("Access to project denied");

        var query = _context.Tasks
            .Include(t => t.Status)
            .Include(t => t.Project)
            .Include(t => t.SubTasks.OrderBy(s => s.Order))
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
            .Include(t => t.LabelAssignments).ThenInclude(la => la.Label)
            .Where(t => t.ProjectId == projectId && !t.IsArchived);

        if (statusId.HasValue) query = query.Where(t => t.StatusId == statusId.Value);
        if (assigneeId.HasValue) query = query.Where(t => t.AssigneeId == assigneeId.Value);
        if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);

        return await query.OrderBy(t => t.Status!.Order).ThenBy(t => t.Order).ThenBy(t => t.CreatedAt).ToListAsync();
    }

    public async System.Threading.Tasks.Task<TaskItem?> GetTaskByIdAsync(Guid taskId, Guid userId)
    {
        var task = await _context.Tasks
            .Include(t => t.Status)
            .Include(t => t.Project).ThenInclude(p => p!.Workspace)
            .Include(t => t.SubTasks.OrderBy(s => s.Order))
            .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
            .Include(t => t.LabelAssignments).ThenInclude(la => la.Label)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return null;
        if (!await _workspaceService.HasAccessToWorkspaceAsync(task.Project!.WorkspaceId, userId))
            throw new UnauthorizedAccessException("Access to task denied");
        return task;
    }

    public async System.Threading.Tasks.Task<TaskItem?> UpdateTaskAsync(Guid taskId, string title, string? description = null,
        TaskPriority? priority = null, DateTime? dueDate = null, Dictionary<string, string>? stickerValues = null,
        Guid userId = default)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return null;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        task.Title = title.Trim();
        task.Description = description?.Trim();
        task.UpdatedAt = DateTime.UtcNow;
        if (priority.HasValue) task.Priority = priority.Value;
        task.DueDate = dueDate;
        if (stickerValues != null)
            task.StickerValues = JsonSerializer.Serialize(stickerValues);

        await _context.SaveChangesAsync();
        _logger.LogInformation("Task updated: {TaskId}", taskId);
        return task;
    }

    public async System.Threading.Tasks.Task<bool> DeleteTaskAsync(Guid taskId, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);
        task.IsArchived = true;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(task.ProjectId);
        await _context.SaveChangesAsync();
        return true;
    }

    public async System.Threading.Tasks.Task<bool> MoveTaskAsync(Guid taskId, Guid newStatusId, int newOrder, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        var newStatus = await _context.ProjectStatuses.FirstOrDefaultAsync(s => s.Id == newStatusId && s.ProjectId == task.ProjectId);
        if (newStatus == null) throw new ArgumentException("Invalid status for this project");

        var oldStatusId = task.StatusId;
        var oldOrder = task.Order;
        var oldStatusWasFinal = task.Status?.IsFinal ?? false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (oldStatusId == newStatusId)
            {
                await ReorderTasksInStatus(newStatusId, oldOrder, newOrder);
            }
            else
            {
                await _context.Tasks.Where(t => t.StatusId == oldStatusId && t.Order > oldOrder)
                    .ExecuteUpdateAsync(t => t.SetProperty(x => x.Order, x => x.Order - 1));
                await _context.Tasks.Where(t => t.StatusId == newStatusId && t.Order >= newOrder)
                    .ExecuteUpdateAsync(t => t.SetProperty(x => x.Order, x => x.Order + 1));
            }

            if (oldStatusId != newStatusId && !oldStatusWasFinal && newStatus.IsFinal)
            {
                await EnqueueGamificationEventAsync(
                    "task-completed",
                    () => _gamificationHook!.OnTaskCompleted(task.Project!.WorkspaceId, taskId, userId, task.ProjectId, task.Priority),
                    taskId,
                    userId,
                    task.ProjectId);
            }
            else if (oldStatusId != newStatusId && oldStatusWasFinal && !newStatus.IsFinal)
            {
                await EnqueueGamificationEventAsync(
                    "task-reopened",
                    () => _gamificationHook!.OnTaskReopened(task.Project!.WorkspaceId, taskId, userId, task.ProjectId),
                    taskId,
                    userId,
                    task.ProjectId);
            }

            task.StatusId = newStatusId;
            task.Order = newOrder;
            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(task.ProjectId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async System.Threading.Tasks.Task<bool> AssignTaskAsync(Guid taskId, Guid? assigneeId, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);
        task.AssigneeId = assigneeId;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async System.Threading.Tasks.Task<TaskItem?> UpdateTaskStickersAsync(Guid taskId, Dictionary<string, string> stickerValues, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return null;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        // Сливаем входящие значения стикеров с уже сохранёнными.
        var existing = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(task.StickerValues))
        {
            existing = JsonSerializer.Deserialize<Dictionary<string, string>>(task.StickerValues) ?? new();
        }
        foreach (var kv in stickerValues)
        {
            if (string.IsNullOrEmpty(kv.Value))
                existing.Remove(kv.Key); // Пустое значение означает удаление стикера.
            else
                existing[kv.Key] = kv.Value;
        }
        task.StickerValues = existing.Count > 0 ? JsonSerializer.Serialize(existing) : null;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Task stickers updated: {TaskId}", taskId);
        return task;
    }

    private async System.Threading.Tasks.Task ReorderTasksInStatus(Guid statusId, int fromOrder, int toOrder)
    {
        if (fromOrder == toOrder) return;
        if (fromOrder < toOrder)
        {
            await _context.Tasks.Where(t => t.StatusId == statusId && t.Order > fromOrder && t.Order <= toOrder)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.Order, x => x.Order - 1));
        }
        else
        {
            await _context.Tasks.Where(t => t.StatusId == statusId && t.Order >= toOrder && t.Order < fromOrder)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.Order, x => x.Order + 1));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Операции с подзадачами.
    // ═══════════════════════════════════════════════════════════════════════
    public async System.Threading.Tasks.Task<SubTaskItem?> GetSubTaskByIdAsync(Guid subtaskId, Guid userId)
    {
        var sub = await _context.SubTasks.Include(s => s.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(s => s.Id == subtaskId);
        if (sub == null) return null;
        await _workspaceService.RequireTaskMutationPermissionAsync(sub.Task!.Project!.WorkspaceId, userId);
        return sub;
    }
    public async System.Threading.Tasks.Task<SubTaskItem> CreateSubTaskAsync(Guid taskId, string title, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) throw new ArgumentException("Task not found");
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        var maxOrder = task.SubTasks.Count > 0 ? task.SubTasks.Max(s => s.Order) : 0;

        var sub = new SubTaskItem
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Title = title.Trim(),
            IsCompleted = false,
            Order = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.SubTasks.Add(sub);
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(task.ProjectId);
        await _context.SaveChangesAsync();
        return sub;
    }

    public async System.Threading.Tasks.Task<SubTaskItem?> UpdateSubTaskAsync(Guid subtaskId, string? title, bool? isCompleted, Guid userId)
    {
        var sub = await _context.SubTasks.Include(s => s.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(s => s.Id == subtaskId);
        if (sub == null) return null;

        await _workspaceService.RequireTaskMutationPermissionAsync(sub.Task!.Project!.WorkspaceId, userId);

        if (title != null) sub.Title = title.Trim();
        var wasCompleted = sub.IsCompleted;
        if (isCompleted.HasValue) sub.IsCompleted = isCompleted.Value;
        sub.Task!.UpdatedAt = DateTime.UtcNow;

        if (!wasCompleted && sub.IsCompleted)
        {
            await EnqueueGamificationEventAsync(
                "subtask-completed",
                () => _gamificationHook!.OnSubtaskCompleted(sub.Task!.Project!.WorkspaceId, subtaskId, userId, sub.Task!.ProjectId),
                subtaskId,
                userId,
                sub.Task!.ProjectId);
        }

        await _context.SaveChangesAsync();
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(sub.Task!.ProjectId);
        await _context.SaveChangesAsync();

        return sub;
    }

    public async System.Threading.Tasks.Task<bool> DeleteSubTaskAsync(Guid subtaskId, Guid userId)
    {
        var sub = await _context.SubTasks.Include(s => s.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(s => s.Id == subtaskId);
        if (sub == null) return false;

        await _workspaceService.RequireTaskMutationPermissionAsync(sub.Task!.Project!.WorkspaceId, userId);

        sub.Task!.UpdatedAt = DateTime.UtcNow;
        _context.SubTasks.Remove(sub);
        await _context.SaveChangesAsync();
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(sub.Task.ProjectId);
        await _context.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Операции с комментариями.
    // ═══════════════════════════════════════════════════════════════════════
    public async System.Threading.Tasks.Task<TaskComment?> GetCommentByIdAsync(Guid commentId, Guid userId)
    {
        var comment = await _context.TaskComments.Include(c => c.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment == null) return null;
        await _workspaceService.RequireTaskMutationPermissionAsync(comment.Task!.Project!.WorkspaceId, userId);
        return comment;
    }
    public async System.Threading.Tasks.Task<TaskComment> AddCommentAsync(Guid taskId, string text, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) throw new ArgumentException("Task not found");
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        var comment = new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AuthorId = userId,
            Text = text.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TaskComments.Add(comment);
        task.UpdatedAt = DateTime.UtcNow;

        await EnqueueGamificationEventAsync(
            "comment-added",
            () => _gamificationHook!.OnCommentAdded(task.Project!.WorkspaceId, taskId, userId, task.ProjectId),
            taskId,
            userId,
            task.ProjectId);

        await _context.SaveChangesAsync();

        return comment;
    }

    public async System.Threading.Tasks.Task<TaskComment?> UpdateCommentAsync(Guid commentId, string text, Guid userId)
    {
        var comment = await _context.TaskComments.Include(c => c.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment == null) return null;

        await _workspaceService.RequireTaskMutationPermissionAsync(comment.Task!.Project!.WorkspaceId, userId);

        // Редактировать комментарий может только его автор.
        if (comment.AuthorId != userId)
            throw new UnauthorizedAccessException("Only the author can edit this comment");

        comment.Text = text.Trim();
        comment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return comment;
    }

    public async System.Threading.Tasks.Task<bool> DeleteCommentAsync(Guid commentId, Guid userId)
    {
        var comment = await _context.TaskComments.Include(c => c.Task).ThenInclude(t => t!.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment == null) return false;

        await _workspaceService.RequireTaskMutationPermissionAsync(comment.Task!.Project!.WorkspaceId, userId);

        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Операции с метками.
    // ═══════════════════════════════════════════════════════════════════════

    public async System.Threading.Tasks.Task<ProjectLabel> CreateProjectLabelAsync(Guid projectId, string name, string color, Guid userId)
    {
        var project = await _context.Projects.Include(p => p.Workspace).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new ArgumentException("Project not found");
        await _workspaceService.RequireTaskMutationPermissionAsync(project.WorkspaceId, userId);

        var maxOrder = await _context.ProjectLabels.Where(l => l.ProjectId == projectId).MaxAsync(l => (int?)l.Order) ?? 0;

        var label = new ProjectLabel
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name.Trim(),
            Color = color,
            Order = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProjectLabels.Add(label);
        await _context.SaveChangesAsync();
        return label;
    }

    public async System.Threading.Tasks.Task<IEnumerable<ProjectLabel>> GetProjectLabelsAsync(Guid projectId, Guid userId)
    {
        var project = await _context.Projects.Include(p => p.Workspace).FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new ArgumentException("Project not found");
        if (!await _workspaceService.HasAccessToWorkspaceAsync(project.WorkspaceId, userId))
            throw new UnauthorizedAccessException("Access denied");

        return await _context.ProjectLabels.Where(l => l.ProjectId == projectId).OrderBy(l => l.Order).ToListAsync();
    }

    public async System.Threading.Tasks.Task<ProjectLabel?> UpdateProjectLabelAsync(Guid labelId, string? name, string? color, Guid userId)
    {
        var label = await _context.ProjectLabels.Include(l => l.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(l => l.Id == labelId);
        if (label == null) return null;
        await _workspaceService.RequireTaskMutationPermissionAsync(label.Project!.WorkspaceId, userId);

        if (name != null) label.Name = name.Trim();
        if (color != null) label.Color = color;
        await _context.SaveChangesAsync();
        return label;
    }

    public async System.Threading.Tasks.Task<bool> DeleteProjectLabelAsync(Guid labelId, Guid userId)
    {
        var label = await _context.ProjectLabels.Include(l => l.Project).ThenInclude(p => p!.Workspace)
            .FirstOrDefaultAsync(l => l.Id == labelId);
        if (label == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(label.Project!.WorkspaceId, userId);

        _context.ProjectLabels.Remove(label);
        await _context.SaveChangesAsync();
        return true;
    }

    public async System.Threading.Tasks.Task<bool> AssignLabelToTaskAsync(Guid taskId, Guid labelId, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        var label = await _context.ProjectLabels.FirstOrDefaultAsync(l => l.Id == labelId && l.ProjectId == task.ProjectId);
        if (label == null) throw new ArgumentException("Label not found in this project");

        var exists = await _context.TaskLabelAssignments.AnyAsync(a => a.TaskId == taskId && a.LabelId == labelId);
        if (exists) return true; // Метка уже привязана.

        _context.TaskLabelAssignments.Add(new TaskLabelAssignment
        {
            TaskId = taskId,
            LabelId = labelId,
            AssignedAt = DateTime.UtcNow
        });
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async System.Threading.Tasks.Task<bool> RemoveLabelFromTaskAsync(Guid taskId, Guid labelId, Guid userId)
    {
        var task = await GetTaskByIdAsync(taskId, userId);
        if (task == null) return false;
        await _workspaceService.RequireTaskMutationPermissionAsync(task.Project!.WorkspaceId, userId);

        var assignment = await _context.TaskLabelAssignments.FirstOrDefaultAsync(a => a.TaskId == taskId && a.LabelId == labelId);
        if (assignment == null) return true; // Метка уже отвязана.

        _context.TaskLabelAssignments.Remove(assignment);
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task EnqueueGamificationEventAsync(
        string eventType,
        Func<Task> enqueueAction,
        Guid entityId,
        Guid userId,
        Guid projectId)
    {
        if (_gamificationHook is null)
        {
            _logger.LogInformation(
                "Gamification side-effect skipped because the integration is disabled. EventType={EventType} EntityId={EntityId} UserId={UserId} ProjectId={ProjectId}",
                eventType,
                entityId,
                userId,
                projectId);
            return;
        }

        await enqueueAction();

        _logger.LogDebug(
            "Queued gamification event in integration outbox. EventType={EventType} EntityId={EntityId} UserId={UserId} ProjectId={ProjectId}",
            eventType,
            entityId,
            userId,
            projectId);
    }
}
