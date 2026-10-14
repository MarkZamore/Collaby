using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// ProjectStatus - статус/колонка для задач (Kanban)
/// </summary>
public class ProjectStatus
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid ProjectId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // "Backlog", "Todo", "In Progress", "Done"
    
    public int Order { get; set; } = 0; // Порядок колонок
    
    [MaxLength(7)]
    public string? Color { get; set; } = "#808080"; // HEX цвет
    
    public bool IsFinal { get; set; } = false; // Закрывающий статус?
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Внешний ключ.
    [ForeignKey(nameof(ProjectId))]
    public virtual Project? Project { get; set; }
    
    // Навигационные свойства.
    public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}
