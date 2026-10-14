namespace GamificationService.Domain.Entities;

public class SkillTreeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public int Tier { get; set; } // Номер слоя развития, например 1, 2 или 3.
    public string Branch { get; set; } = "A"; // Ветка дерева, например A или B.
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RewardJson { get; set; } = "{}"; // JSON-награда, например {"xpMultiplier":1.1} или {"badge":"specialist"}.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
