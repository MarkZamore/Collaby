using ManagementService.Platform.Correlation;
using Microsoft.AspNetCore.Http;

namespace ManagementService.Platform.InternalAdmin;

public static class InternalAdminRequestExtensions
{
    public static bool TryGetRequiredCorrelationId(this HttpRequest request, out string correlationId)
    {
        correlationId = string.Empty;

        if (!request.Headers.TryGetValue(CorrelationHeaderNames.CorrelationId, out var values))
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
