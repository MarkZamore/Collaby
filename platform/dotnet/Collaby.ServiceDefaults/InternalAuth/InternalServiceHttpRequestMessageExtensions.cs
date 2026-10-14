using System.Net.Http;
using Collaby.ServiceDefaults.Correlation;

namespace Collaby.ServiceDefaults.InternalAuth;

public static class InternalServiceHttpRequestMessageExtensions
{
    public static void AddInternalServiceAuthentication(
        this HttpRequestMessage request,
        string callerService,
        string credential,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(callerService))
        {
            throw new InvalidOperationException("Internal service caller name must be configured.");
        }

        if (string.IsNullOrWhiteSpace(credential))
        {
            throw new InvalidOperationException($"Internal credential for caller '{callerService}' must be configured.");
        }

        request.Headers.TryAddWithoutValidation(InternalAuthHeaderNames.CallerService, callerService);
        request.Headers.TryAddWithoutValidation(InternalAuthHeaderNames.Credential, credential);

        var effectiveCorrelationId = !string.IsNullOrWhiteSpace(correlationId)
            ? correlationId.Trim()
            : request.Headers.TryGetValues(CorrelationHeaderNames.CorrelationId, out var existingValues)
                ? existingValues.FirstOrDefault()
                : CorrelationContext.GetOrCreate();

        if (!string.IsNullOrWhiteSpace(effectiveCorrelationId))
        {
            request.Headers.Remove(CorrelationHeaderNames.CorrelationId);
            request.Headers.TryAddWithoutValidation(CorrelationHeaderNames.CorrelationId, effectiveCorrelationId);
        }
    }
}
