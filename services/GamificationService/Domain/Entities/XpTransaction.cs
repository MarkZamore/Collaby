using GamificationService.Domain.Enums;

namespace GamificationService.Domain.Entities;

public class XpTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public long Amount { get; set; }
    public long CoinsAmount { get; set; }
    public XpReason Reason { get; set; }
    public Guid? TaskId { get; set; }
    public string Status { get; set; } = "Applied";
    public string? Metadata { get; set; } // JSON с дополнительным контекстом начисления или отката.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
