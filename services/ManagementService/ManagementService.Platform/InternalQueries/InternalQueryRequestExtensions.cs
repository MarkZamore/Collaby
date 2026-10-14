namespace ManagementService.Platform.InternalQueries;

public static class InternalQueryRequestExtensions
{
    private static readonly HttpRequestOptionsKey<bool> RetryableIdempotentQueryKey =
        new("Collaby.RetryableIdempotentInternalQuery");

    public static void MarkAsRetryableIdempotentInternalQuery(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Options.Set(RetryableIdempotentQueryKey, true);
    }

    internal static bool IsRetryableIdempotentInternalQuery(this HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(RetryableIdempotentQueryKey, out var isRetryable) && isRetryable;
    }
}
