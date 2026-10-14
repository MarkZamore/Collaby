using Microsoft.AspNetCore.Http;

namespace Collaby.InternalContracts.InternalAdmin;

public static class InternalAdminRequestExtensions
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public static bool TryGetRequiredCorrelationId(this HttpRequest request, out string correlationId)
    {
        correlationId = string.Empty;

        if (!request.Headers.TryGetValue(CorrelationIdHeaderName, out var values))
        {
            return false;
        }

        var candidate = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        correlationId = candidate.Trim();
        return true;
    }
}
