using System.Threading;

namespace Collaby.ServiceDefaults.Health;

public sealed class ServiceReadinessState
{
    private int _isReady;
    private string? _statusDetail;

    public bool IsReady => Volatile.Read(ref _isReady) == 1;

    public string? StatusDetail => Volatile.Read(ref _statusDetail);

    public string? FailureReason => StatusDetail;

    public void MarkStarting(string stage)
    {
        Volatile.Write(ref _statusDetail, stage);
        Volatile.Write(ref _isReady, 0);
    }

    public void MarkReady()
    {
        Volatile.Write(ref _statusDetail, null);
        Volatile.Write(ref _isReady, 1);
    }

    public void MarkFailed(string? reason)
    {
        Volatile.Write(ref _statusDetail, reason);
        Volatile.Write(ref _isReady, 0);
    }
}
