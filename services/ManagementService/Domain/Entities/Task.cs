using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// TaskItem - минимальная единица работы
/// </summary>
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid ProjectId { get; set; }
    
    public Guid? StatusId { get; set; } // ProjectStatus
    
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(5000)]
    public string? Description { get; set; }
    
    public Guid? AssigneeId { get; set; } // UserId from IdentityService
    
    [Required]
    public Guid CreatedBy { get; set; } // UserId from IdentityService
    
    /// <summary>
    /// Порядковый номер внутри статуса для drag&drop
    /// </summary>
    public int Order { get; set; } = 0;
    
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// JSON-хранилище значений стикеров, напр. {"priority":"critical","task-type":"bug"}
    /// </summary>
    [MaxLength(4000)]
    public string? StickerValues { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsArchived { get; set; } = false;
    
    // Внешние ключи.
    [ForeignKey(nameof(ProjectId))]
    public virtual Project? Project { get; set; }
    
    [ForeignKey(nameof(StatusId))]
    public virtual ProjectStatus? Status { get; set; }

    // Навигация к дочерним коллекциям.
    public virtual ICollection<SubTaskItem> SubTasks { get; set; } = new List<SubTaskItem>();
    public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public virtual ICollection<TaskLabelAssignment> LabelAssignments { get; set; } = new List<TaskLabelAssignment>();
}

/// <summary>
/// Приоритеты задач
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}
