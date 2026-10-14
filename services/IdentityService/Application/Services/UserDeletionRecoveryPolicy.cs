using IdentityService.Domain.Entities;

namespace IdentityService.Application.Services;

public static class UserDeletionRecoveryPolicy
{
    private static readonly TimeSpan StaleOperationThreshold = TimeSpan.FromMinutes(5);

    public static bool CanRetry(UserDeletionOperation operation, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        if (string.Equals(operation.Status, UserDeletionOperationStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsRecoverableInFlightStatus(operation.Status))
        {
            return false;
        }

        var nonTerminalSteps = operation.Steps.Where(step => !IsTerminalStepStatus(step.Status)).ToArray();
        if (nonTerminalSteps.Length == 0)
        {
            return false;
        }

        var staleCutoff = now.Subtract(StaleOperationThreshold);
        if (operation.UpdatedAtUtc <= staleCutoff)
        {
            return true;
        }

        if (nonTerminalSteps.Any(step => GetStepProgressTimestamp(step, operation) <= staleCutoff))
        {
            return true;
        }

        return false;
    }

    public static string GetRetryLabel(UserDeletionOperation operation, DateTime? utcNow = null)
    {
        return string.Equals(operation.Status, UserDeletionOperationStatuses.Failed, StringComparison.OrdinalIgnoreCase)
            ? "retry_failed_steps"
            : "resume_stale_deletion";
    }

    public static void PrepareForRetry(UserDeletionOperation operation, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;

        operation.Status = UserDeletionOperationStatuses.RetryRequested;
        operation.FailureMessage = null;
        operation.CompletedAtUtc = null;
        operation.UpdatedAtUtc = now;
        operation.RetryCount += 1;

        foreach (var step in operation.Steps.Where(step => !IsTerminalStepStatus(step.Status)))
        {
            step.Status = UserDeletionOperationStepStatuses.Pending;
            step.LastError = null;
            step.CompletedAtUtc = null;
            step.StartedAtUtc = null;
        }
    }

    private static bool IsRecoverableInFlightStatus(string status)
    {
        return string.Equals(status, UserDeletionOperationStatuses.Pending, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, UserDeletionOperationStatuses.Running, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, UserDeletionOperationStatuses.RetryRequested, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTerminalStepStatus(string status)
    {
        return string.Equals(status, UserDeletionOperationStepStatuses.Succeeded, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, UserDeletionOperationStepStatuses.Noop, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetStepProgressTimestamp(UserDeletionOperationStep step, UserDeletionOperation operation)
    {
        return step.StartedAtUtc ??
               operation.StartedAtUtc ??
               operation.RequestedAtUtc;
    }
}
