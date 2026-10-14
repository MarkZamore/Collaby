namespace IdentityService.Infrastructure.Health;

public sealed class IdentityServiceReadinessState
{
    private volatile bool _isReady;

    public bool IsReady => _isReady;

    public string? FailureReason { get; private set; }

    public void MarkReady()
    {
        FailureReason = null;
        _isReady = true;
    }

    public void MarkFailed(string reason)
    {
        FailureReason = reason;
        _isReady = false;
    }
}
