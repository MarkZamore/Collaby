using ManagementService.Domain.Entities;

namespace ManagementService.Contracts.Responses;

/// <summary>
/// Ответ с данными о задаче
/// </summary>
public class TaskResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? AssigneeId { get; set; }
    public int Order { get; set; }
    public TaskPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsArchived { get; set; }
    public Dictionary<string, string>? StickerValues { get; set; }

    // Дочерние коллекции.
    public List<SubTaskResponse> SubTasks { get; set; } = new();
    public List<CommentResponse> Comments { get; set; } = new();
    public List<LabelResponse> Labels { get; set; } = new();
}

/// <summary>
/// Ответ с данными о подзадаче
/// </summary>
public class SubTaskResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Ответ с данными о комментарии
/// </summary>
public class CommentResponse
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Ответ с данными о метке
/// </summary>
public class LabelResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";
}
