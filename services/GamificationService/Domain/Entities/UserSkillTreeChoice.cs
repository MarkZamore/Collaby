namespace GamificationService.Domain.Entities;

public class UserSkillTreeChoice
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public int Tier { get; set; }
    public Guid NodeId { get; set; }
    public DateTime ChosenAt { get; set; } = DateTime.UtcNow;

    public virtual SkillTreeNode? Node { get; set; }
}
