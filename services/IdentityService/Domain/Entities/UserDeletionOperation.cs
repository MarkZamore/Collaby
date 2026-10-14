using System.ComponentModel.DataAnnotations;

namespace IdentityService.Domain.Entities;

public sealed class UserDeletionOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [MaxLength(255)]
    public string UserEmail { get; set; } = string.Empty;

    public Guid? RequestedByUserId { get; set; }

    [MaxLength(255)]
    public string? RequestedByEmail { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = UserDeletionOperationStatuses.Pending;

    [MaxLength(128)]
    public string CorrelationId { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(2000)]
    public string? FailureMessage { get; set; }

    public int RetryCount { get; set; }

    public ICollection<UserDeletionOperationStep> Steps { get; set; } = new List<UserDeletionOperationStep>();
}

public sealed class UserDeletionOperationStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OperationId { get; set; }
    public UserDeletionOperation Operation { get; set; } = null!;

    [MaxLength(128)]
    public string ServiceName { get; set; } = string.Empty;

    public int StepOrder { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = UserDeletionOperationStepStatuses.Pending;

    public int AttemptCount { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}

public static class UserDeletionOperationStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string RetryRequested = "retry_requested";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}

public static class UserDeletionOperationStepStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Noop = "noop";
}
