namespace Collaby.InternalContracts.Events;

public sealed record ManagementProjectProgressSnapshotEventV1(
    Guid ProjectId,
    double ProgressPercent,
    double TotalWeight,
    double CompletedWeight,
    int TotalTasks,
    int CompletedTasks,
    int TotalSubtasks,
    int CompletedSubtasks,
    DateTime UpdatedAtUtc);
