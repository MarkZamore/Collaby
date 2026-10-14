using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// Подзадача — чекбокс-элемент внутри задачи
/// </summary>
public class SubTaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TaskId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; } = false;

    public int Order { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TaskId))]
    public virtual TaskItem? Task { get; set; }
}
