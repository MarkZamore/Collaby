using System.ComponentModel.DataAnnotations;

namespace ManagementService.Domain.Entities;

public sealed class ServiceRestartOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(128)]
    public string TargetService { get; set; } = string.Empty;

    public string OwnedStateUnitsJson { get; set; } = "[]";

    [MaxLength(64)]
    public string Status { get; set; } = ServiceRestartOperationStatuses.Queued;

    public Guid? RequestedByUserId { get; set; }

    [MaxLength(255)]
    public string? RequestedByEmail { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public long? DeploymentGeneration { get; set; }

    [MaxLength(512)]
    public string? DeploymentUid { get; set; }

    [MaxLength(128)]
    public string? RolloutStatus { get; set; }

    [MaxLength(128)]
    public string? DeploymentNamespace { get; set; }

    [MaxLength(2000)]
    public string? FailureMessage { get; set; }
}

public static class ServiceRestartOperationStatuses
{
    public const string Queued = "queued";
    public const string Triggered = "triggered";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}
