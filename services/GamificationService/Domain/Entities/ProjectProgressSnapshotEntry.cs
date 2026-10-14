namespace GamificationService.Domain.Entities;

public sealed class ProjectProgressSnapshotEntry
{
    public Guid ProjectId { get; set; }
    public double ProgressPercent { get; set; }
    public double TotalWeight { get; set; }
    public double CompletedWeight { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int TotalSubtasks { get; set; }
    public int CompletedSubtasks { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
