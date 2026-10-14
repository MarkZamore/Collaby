using System.Threading;

namespace ManagementService.Platform.Health;

public sealed class ServiceReadinessState
{
    private int _isReady;
    private string? _failureReason;

    public bool IsReady => Volatile.Read(ref _isReady) == 1;

    public string? FailureReason => Volatile.Read(ref _failureReason);

    public void MarkReady()
    {
        Volatile.Write(ref _failureReason, null);
        Volatile.Write(ref _isReady, 1);
    }

    public void MarkFailed(string? reason)
    {
        Volatile.Write(ref _failureReason, reason);
        Volatile.Write(ref _isReady, 0);
    }
}
