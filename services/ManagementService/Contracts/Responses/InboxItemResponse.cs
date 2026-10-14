using ManagementService.Domain.Entities;

namespace ManagementService.Contracts.Responses;

/// <summary>
/// Элемент входящих: задача «к выполнению», назначенная текущему пользователю, с контекстом воркспейса и проекта.
/// </summary>
public class InboxItemResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string WorkspaceName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
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

    public List<SubTaskResponse> SubTasks { get; set; } = new();
    public List<CommentResponse> Comments { get; set; } = new();
    public List<LabelResponse> Labels { get; set; } = new();
}
