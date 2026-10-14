using GamificationService.Domain.Enums;

namespace GamificationService.Domain.Entities;

public class Quest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public QuestType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int XpReward { get; set; }
    public int CoinReward { get; set; }
    public string Requirement { get; set; } = "{}"; // JSON-описание условия, например {"type":"complete_tasks","count":3}.
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
