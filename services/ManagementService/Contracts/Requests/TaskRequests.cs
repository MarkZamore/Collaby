using ManagementService.Domain.Entities;

namespace ManagementService.Contracts.Requests;

public class CreateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public Dictionary<string, string>? StickerValues { get; set; }
}

public class UpdateTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }
    public Dictionary<string, string>? StickerValues { get; set; }
}

public class MoveTaskRequest
{
    public Guid StatusId { get; set; }
    public int Order { get; set; }
}

public class AssignTaskRequest
{
    public Guid? AssigneeId { get; set; }
}

public class GetTasksRequest
{
    public Guid? StatusId { get; set; }
    public Guid? AssigneeId { get; set; }
    public TaskPriority? Priority { get; set; }
}

// ─── SubTask Requests ───────────────────────────────────────────────────────

public class CreateSubTaskRequest
{
    public string Title { get; set; } = string.Empty;
}

public class UpdateSubTaskRequest
{
    public string? Title { get; set; }
    public bool? IsCompleted { get; set; }
}

// ─── Comment Requests ───────────────────────────────────────────────────────

public class CreateCommentRequest
{
    public string Text { get; set; } = string.Empty;
}

public class UpdateCommentRequest
{
    public string Text { get; set; } = string.Empty;
}

// ─── Label Requests ─────────────────────────────────────────────────────────

public class CreateLabelRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";
}

public class UpdateLabelRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

public class AssignLabelRequest
{
    public Guid LabelId { get; set; }
}

// ─── Sticker Requests ───────────────────────────────────────────────────────

public class UpdateTaskStickersRequest
{
    public Dictionary<string, string> StickerValues { get; set; } = new();
}

public class UpdateBoardStickersRequest
{
    public List<string> BoardStickerIds { get; set; } = new();
}