using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Security;
using ManagementService.Application.Services;
using ManagementService.Contracts.Requests;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;
using ManagementService.Domain.Enums;

namespace ManagementService.Controllers;

/// <summary>
/// API контроллер для управления задачами
/// </summary>
[ApiController]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IRealtimeNotifier _notifier;
    private readonly INotificationService _notifications;
    private readonly ILogger<TaskController> _logger;

    public TaskController(ITaskService taskService, IRealtimeNotifier notifier, INotificationService notifications, ILogger<TaskController> logger)
    {
        _taskService = taskService;
        _notifier = notifier;
        _notifications = notifications;
        _logger = logger;
    }

    private ActionResult ForbiddenWithCode(WorkspacePermissionException ex)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            code = ex.Code,
            error = ex.Message,
        });
    }

    private ActionResult ForbidOrCode(UnauthorizedAccessException ex)
    {
        if (ex is WorkspacePermissionException permissionException)
        {
            return ForbiddenWithCode(permissionException);
        }

        return Forbid();
    }

    /// <summary>
    /// Получить ID текущего пользователя из JWT токена
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
            ?? User.FindFirst("sub") 
            ?? User.FindFirst("userId");
        
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        }

        return userId;
    }

    /// <summary>
    /// Создать новую задачу в проекте
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="request">Данные для создания задачи</param>
    /// <returns>Созданная задача</returns>
    [HttpPost("projects/{projectId}/tasks")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskResponse>> CreateTask(
        [FromRoute] Guid projectId, 
        [FromBody] CreateTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.CreateTaskAsync(
                projectId, 
                request.Title, 
                request.Description, 
                request.AssigneeId, 
                request.Priority, 
                request.DueDate, 
                request.StickerValues,
                userId);

            _logger.LogInformation($"User {userId} created task {task.Id} in project {projectId}");

            var response = MapToTaskResponse(task);
            _ = _notifier.TaskCreated(projectId, response);

            // Notify assignee if assigned during creation
            if (task.AssigneeId.HasValue && task.AssigneeId.Value != userId)
            {
                await _notifications.CreateAsync(
                    task.AssigneeId.Value,
                    NotificationType.TaskAssigned,
                    "Вам назначена задача",
                    task.Title,
                    referenceId: task.Id,
                    secondaryReferenceId: projectId,
                    actorId: userId);
            }

            return CreatedAtAction(nameof(GetTask), new { taskId = task.Id }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить задачи проекта с фильтрацией
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="statusId">Фильтр по статусу</param>
    /// <param name="assigneeId">Фильтр по назначенному пользователю</param>
    /// <param name="priority">Фильтр по приоритету</param>
    /// <returns>Список задач</returns>
    [HttpGet("projects/{projectId}/tasks")]
    [ProducesResponseType(typeof(IEnumerable<TaskResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<TaskResponse>>> GetProjectTasks(
        [FromRoute] Guid projectId,
        [FromQuery] Guid? statusId = null,
        [FromQuery] Guid? assigneeId = null,
        [FromQuery] TaskPriority? priority = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tasks = await _taskService.GetProjectTasksAsync(projectId, statusId, assigneeId, priority, userId);

            var response = tasks.Select(MapToTaskResponse);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting project tasks: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить задачу по ID
    /// </summary>
    /// <param name="taskId">ID задачи</param>
    /// <returns>Задача</returns>
    [HttpGet("tasks/{taskId}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskResponse>> GetTask([FromRoute] Guid taskId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.GetTaskByIdAsync(taskId, userId);

            if (task == null)
            {
                return NotFound();
            }

            var response = MapToTaskResponse(task);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить задачу
    /// </summary>
    /// <param name="taskId">ID задачи</param>
    /// <param name="request">Данные для обновления</param>
    /// <returns>Обновленная задача</returns>
    [HttpPut("tasks/{taskId}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskResponse>> UpdateTask(
        [FromRoute] Guid taskId, 
        [FromBody] UpdateTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.UpdateTaskAsync(
                taskId, 
                request.Title, 
                request.Description, 
                request.Priority, 
                request.DueDate, 
                request.StickerValues,
                userId);

            if (task == null)
            {
                return NotFound();
            }

            _logger.LogInformation($"User {userId} updated task {taskId}");

            var response = MapToTaskResponse(task);
            _ = _notifier.TaskUpdated(task.ProjectId, response);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Удалить задачу
    /// </summary>
    /// <param name="taskId">ID задачи</param>
    /// <returns>Результат операции</returns>
    [HttpDelete("tasks/{taskId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTask([FromRoute] Guid taskId)
    {
        try
        {
            var userId = GetCurrentUserId();
            // Get projectId before deleting so we can broadcast
            var taskEntity = await _taskService.GetTaskByIdAsync(taskId, userId);
            var projectId = taskEntity?.ProjectId;

            var deleted = await _taskService.DeleteTaskAsync(taskId, userId);

            if (!deleted)
            {
                return NotFound();
            }

            if (projectId.HasValue)
                _ = _notifier.TaskDeleted(projectId.Value, taskId);

            _logger.LogInformation($"User {userId} deleted task {taskId}");
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Переместить задачу в другой статус
    /// </summary>
    /// <param name="taskId">ID задачи</param>
    /// <param name="request">Данные для перемещения</param>
    /// <returns>Результат операции</returns>
    [HttpPut("tasks/{taskId}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MoveTask(
        [FromRoute] Guid taskId, 
        [FromBody] MoveTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var taskEntity = await _taskService.GetTaskByIdAsync(taskId, userId);
            var moved = await _taskService.MoveTaskAsync(taskId, request.StatusId, request.Order, userId);

            if (!moved)
            {
                return NotFound();
            }

            if (taskEntity != null)
                _ = _notifier.TaskMoved(taskEntity.ProjectId, taskId, request.StatusId, request.Order);

            _logger.LogInformation($"User {userId} moved task {taskId} to status {request.StatusId}");
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Invalid argument: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error moving task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Назначить задачу пользователю
    /// </summary>
    /// <param name="taskId">ID задачи</param>
    /// <param name="request">Данные для назначения</param>
    /// <returns>Результат операции</returns>
    [HttpPut("tasks/{taskId}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignTask(
        [FromRoute] Guid taskId, 
        [FromBody] AssignTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var assigned = await _taskService.AssignTaskAsync(taskId, request.AssigneeId, userId);

            if (!assigned)
            {
                return NotFound();
            }

            // Fetch updated task to broadcast
            var taskEntity = await _taskService.GetTaskByIdAsync(taskId, userId);
            if (taskEntity != null)
                _ = _notifier.TaskUpdated(taskEntity.ProjectId, MapToTaskResponse(taskEntity));

            // Notify new assignee
            if (request.AssigneeId.HasValue && request.AssigneeId.Value != userId)
            {
                await _notifications.CreateAsync(
                    request.AssigneeId.Value,
                    NotificationType.TaskAssigned,
                    "Вам назначена задача",
                    taskEntity?.Title ?? "Задача",
                    referenceId: taskId,
                    secondaryReferenceId: taskEntity?.ProjectId,
                    actorId: userId);
            }

            _logger.LogInformation($"User {userId} assigned task {taskId} to user {request.AssigneeId}");
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return ForbidOrCode(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error assigning task: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Маппинг Task -> TaskResponse
    /// </summary>
    private static TaskResponse MapToTaskResponse(TaskItem task)
    {
        return new TaskResponse
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            StatusId = task.StatusId ?? Guid.Empty,
            StatusName = task.Status?.Name ?? "Unknown",
            Title = task.Title,
            Description = task.Description,
            CreatedBy = task.CreatedBy,
            AssigneeId = task.AssigneeId,
            Order = task.Order,
            Priority = task.Priority,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            DueDate = task.DueDate,
            IsArchived = task.IsArchived,
            StickerValues = !string.IsNullOrEmpty(task.StickerValues)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(task.StickerValues)
                : null,
            SubTasks = (task.SubTasks ?? new List<SubTaskItem>()).Select(s => new SubTaskResponse
            {
                Id = s.Id,
                Title = s.Title,
                IsCompleted = s.IsCompleted,
                Order = s.Order,
                CreatedAt = s.CreatedAt,
            }).ToList(),
            Comments = (task.Comments ?? new List<TaskComment>()).Select(c => new CommentResponse
            {
                Id = c.Id,
                AuthorId = c.AuthorId,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            }).ToList(),
            Labels = (task.LabelAssignments ?? new List<TaskLabelAssignment>())
                .Where(la => la.Label != null)
                .Select(la => new LabelResponse
                {
                    Id = la.Label!.Id,
                    Name = la.Label.Name,
                    Color = la.Label.Color,
                }).ToList(),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SubTask Endpoints
    // ═══════════════════════════════════════════════════════════════════════

    [HttpPost("tasks/{taskId}/subtasks")]
    public async Task<ActionResult<SubTaskResponse>> CreateSubTask([FromRoute] Guid taskId, [FromBody] CreateSubTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sub = await _taskService.CreateSubTaskAsync(taskId, request.Title, userId);
            var parentTask = await _taskService.GetTaskByIdAsync(taskId, userId);
            if (parentTask != null)
                _ = _notifier.SubTaskChanged(parentTask.ProjectId, taskId);
            return CreatedAtAction(nameof(GetTask), new { taskId }, new SubTaskResponse
            {
                Id = sub.Id, Title = sub.Title, IsCompleted = sub.IsCompleted, Order = sub.Order, CreatedAt = sub.CreatedAt
            });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("subtasks/{subtaskId}")]
    public async Task<ActionResult<SubTaskResponse>> UpdateSubTask([FromRoute] Guid subtaskId, [FromBody] UpdateSubTaskRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var sub = await _taskService.UpdateSubTaskAsync(subtaskId, request.Title, request.IsCompleted, userId);
            if (sub == null) return NotFound();
            if (sub.TaskId != Guid.Empty)
            {
                var parentTask = await _taskService.GetTaskByIdAsync(sub.TaskId, userId);
                if (parentTask != null)
                    _ = _notifier.SubTaskChanged(parentTask.ProjectId, sub.TaskId);
            }
            return Ok(new SubTaskResponse
            {
                Id = sub.Id, Title = sub.Title, IsCompleted = sub.IsCompleted, Order = sub.Order, CreatedAt = sub.CreatedAt
            });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("subtasks/{subtaskId}")]
    public async Task<IActionResult> DeleteSubTask([FromRoute] Guid subtaskId)
    {
        try
        {
            var userId = GetCurrentUserId();
            // Resolve parent task before deletion for SignalR broadcast
            var sub = await _taskService.GetSubTaskByIdAsync(subtaskId, userId);
            var deleted = await _taskService.DeleteSubTaskAsync(subtaskId, userId);
            if (!deleted) return NotFound();
            if (sub != null)
            {
                var parentTask = await _taskService.GetTaskByIdAsync(sub.TaskId, userId);
                if (parentTask != null)
                    _ = _notifier.SubTaskChanged(parentTask.ProjectId, sub.TaskId);
            }
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Comment Endpoints
    // ═══════════════════════════════════════════════════════════════════════

    [HttpPost("tasks/{taskId}/comments")]
    public async Task<ActionResult<CommentResponse>> AddComment([FromRoute] Guid taskId, [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var comment = await _taskService.AddCommentAsync(taskId, request.Text, userId);
            var parentTask = await _taskService.GetTaskByIdAsync(taskId, userId);
            if (parentTask != null)
            {
                _ = _notifier.CommentChanged(parentTask.ProjectId, taskId);

                // Notify task assignee about new comment (if not self)
                if (parentTask.AssigneeId.HasValue && parentTask.AssigneeId.Value != userId)
                {
                    await _notifications.CreateAsync(
                        parentTask.AssigneeId.Value,
                        NotificationType.CommentAdded,
                        "Новый комментарий к задаче",
                        parentTask.Title,
                        referenceId: taskId,
                        secondaryReferenceId: parentTask.ProjectId,
                        actorId: userId);
                }
            }
            return CreatedAtAction(nameof(GetTask), new { taskId }, new CommentResponse
            {
                Id = comment.Id, AuthorId = comment.AuthorId, Text = comment.Text,
                CreatedAt = comment.CreatedAt, UpdatedAt = comment.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("comments/{commentId}")]
    public async Task<ActionResult<CommentResponse>> UpdateComment([FromRoute] Guid commentId, [FromBody] UpdateCommentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var comment = await _taskService.UpdateCommentAsync(commentId, request.Text, userId);
            if (comment == null) return NotFound();
            var parentTask = await _taskService.GetTaskByIdAsync(comment.TaskId, userId);
            if (parentTask != null)
                _ = _notifier.CommentChanged(parentTask.ProjectId, comment.TaskId);
            return Ok(new CommentResponse
            {
                Id = comment.Id, AuthorId = comment.AuthorId, Text = comment.Text,
                CreatedAt = comment.CreatedAt, UpdatedAt = comment.UpdatedAt
            });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("comments/{commentId}")]
    public async Task<IActionResult> DeleteComment([FromRoute] Guid commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            // Resolve parent task before deletion for SignalR broadcast
            var comment = await _taskService.GetCommentByIdAsync(commentId, userId);
            var deleted = await _taskService.DeleteCommentAsync(commentId, userId);
            if (!deleted) return NotFound();
            if (comment != null)
            {
                var parentTask = await _taskService.GetTaskByIdAsync(comment.TaskId, userId);
                if (parentTask != null)
                    _ = _notifier.CommentChanged(parentTask.ProjectId, comment.TaskId);
            }
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Label Endpoints
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("projects/{projectId}/labels")]
    public async Task<ActionResult<IEnumerable<LabelResponse>>> GetProjectLabels([FromRoute] Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var labels = await _taskService.GetProjectLabelsAsync(projectId, userId);
            return Ok(labels.Select(l => new LabelResponse { Id = l.Id, Name = l.Name, Color = l.Color }));
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("projects/{projectId}/labels")]
    public async Task<ActionResult<LabelResponse>> CreateLabel([FromRoute] Guid projectId, [FromBody] CreateLabelRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var label = await _taskService.CreateProjectLabelAsync(projectId, request.Name, request.Color, userId);
            return CreatedAtAction(nameof(GetProjectLabels), new { projectId }, new LabelResponse
            {
                Id = label.Id, Name = label.Name, Color = label.Color
            });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("labels/{labelId}")]
    public async Task<ActionResult<LabelResponse>> UpdateLabel([FromRoute] Guid labelId, [FromBody] UpdateLabelRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var label = await _taskService.UpdateProjectLabelAsync(labelId, request.Name, request.Color, userId);
            if (label == null) return NotFound();
            return Ok(new LabelResponse { Id = label.Id, Name = label.Name, Color = label.Color });
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("labels/{labelId}")]
    public async Task<IActionResult> DeleteLabel([FromRoute] Guid labelId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var deleted = await _taskService.DeleteProjectLabelAsync(labelId, userId);
            return deleted ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("tasks/{taskId}/labels")]
    public async Task<IActionResult> AssignLabel([FromRoute] Guid taskId, [FromBody] AssignLabelRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _taskService.AssignLabelToTaskAsync(taskId, request.LabelId, userId);
            var taskEntity = await _taskService.GetTaskByIdAsync(taskId, userId);
            if (taskEntity != null)
                _ = _notifier.TaskUpdated(taskEntity.ProjectId, MapToTaskResponse(taskEntity));
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("tasks/{taskId}/labels/{labelId}")]
    public async Task<IActionResult> RemoveLabel([FromRoute] Guid taskId, [FromRoute] Guid labelId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _taskService.RemoveLabelFromTaskAsync(taskId, labelId, userId);
            var taskEntity = await _taskService.GetTaskByIdAsync(taskId, userId);
            if (taskEntity != null)
                _ = _notifier.TaskUpdated(taskEntity.ProjectId, MapToTaskResponse(taskEntity));
            return NoContent();
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Sticker (per-task) Endpoints
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Обновить значения стикеров задачи (merge-семантика: пустое значение = удалить стикер)
    /// </summary>
    [HttpPut("tasks/{taskId}/stickers")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskResponse>> UpdateTaskStickers(
        [FromRoute] Guid taskId, 
        [FromBody] UpdateTaskStickersRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var task = await _taskService.UpdateTaskStickersAsync(taskId, request.StickerValues, userId);
            if (task == null) return NotFound();
            _logger.LogInformation("User {UserId} updated stickers on task {TaskId}", userId, taskId);
            var response = MapToTaskResponse(task);
            _ = _notifier.TaskUpdated(task.ProjectId, response);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex) { return ForbidOrCode(ex); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}
