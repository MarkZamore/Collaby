using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// Project - контейнер для задач внутри workspace
/// </summary>
public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid WorkspaceId { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [Required]
    public Guid CreatedBy { get; set; } // UserId from IdentityService
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsArchived { get; set; } = false;

    /// <summary>
    /// JSON-массив ID стикеров, включённых на доске, напр. ["assignee","deadline","priority"]
    /// </summary>
    [MaxLength(2000)]
    public string? BoardStickerIds { get; set; }
    
    // Внешний ключ.
    [ForeignKey(nameof(WorkspaceId))]
    public virtual Workspace? Workspace { get; set; }
    
    // Навигационные свойства.
    public virtual ICollection<ProjectStatus> Statuses { get; set; } = new List<ProjectStatus>();
    public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public virtual ICollection<ProjectLabel> Labels { get; set; } = new List<ProjectLabel>();
}
