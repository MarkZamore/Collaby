using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// Метка/стикер проекта — определяет доступные метки для задач внутри проекта
/// </summary>
public class ProjectLabel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(7)]
    public string Color { get; set; } = "#808080";

    public int Order { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ProjectId))]
    public virtual Project? Project { get; set; }
}

/// <summary>
/// Связь метки с задачей (many-to-many)
/// </summary>
public class TaskLabelAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TaskId { get; set; }

    [Required]
    public Guid LabelId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TaskId))]
    public virtual TaskItem? Task { get; set; }

    [ForeignKey(nameof(LabelId))]
    public virtual ProjectLabel? Label { get; set; }
}
