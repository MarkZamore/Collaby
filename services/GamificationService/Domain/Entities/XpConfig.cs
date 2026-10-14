namespace GamificationService.Domain.Entities;

public class XpConfig
{
    public Guid ProjectId { get; set; }
    public bool WeightSubtasks { get; set; } = true;
    public bool WeightPriority { get; set; } = true;
    public bool ReviewRequired { get; set; }
    public string FormulaJson { get; set; } = "{}"; // JSON-переопределения формулы для конкретного проекта.
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
