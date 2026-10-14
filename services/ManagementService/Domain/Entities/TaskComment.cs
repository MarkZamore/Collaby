using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// Комментарий к задаче
/// </summary>
public class TaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TaskId { get; set; }

    [Required]
    public Guid AuthorId { get; set; }

    [Required]
    [MaxLength(5000)]
    public string Text { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TaskId))]
    public virtual TaskItem? Task { get; set; }
}
