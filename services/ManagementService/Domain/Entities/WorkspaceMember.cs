using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementService.Domain.Entities;

/// <summary>
/// WorkspaceMember - связь пользователь ↔ workspace с ролями
/// </summary>
public class WorkspaceMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid WorkspaceId { get; set; }
    
    [Required]
    public Guid UserId { get; set; } // UserId from IdentityService
    
    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Member"; // Owner, Admin, Member, Guest
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Invited, Active, Suspended, Left
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    public Guid? InvitedBy { get; set; } // UserId from IdentityService
    
    // Внешний ключ.
    [ForeignKey(nameof(WorkspaceId))]
    public virtual Workspace? Workspace { get; set; }
}
