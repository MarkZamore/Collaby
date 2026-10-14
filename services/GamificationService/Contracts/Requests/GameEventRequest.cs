namespace GamificationService.Contracts.Requests;

public class GameEventRequest
{
    public Guid EventId { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Metadata { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class PurchaseRequest
{
    public Guid ItemId { get; set; }
}

public class SkillTreeChoiceRequest
{
    public Guid NodeId { get; set; }
}

public class SkillTreeConfigNodeRequest
{
    public int Tier { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RewardJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
}

public class UpdateSkillTreeConfigRequest
{
    public List<SkillTreeConfigNodeRequest> Nodes { get; set; } = [];
}
