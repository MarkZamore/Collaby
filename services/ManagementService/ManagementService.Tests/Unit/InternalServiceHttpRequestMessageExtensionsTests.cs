using System.Net.Http;

using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.InternalAuth;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class InternalServiceHttpRequestMessageExtensionsTests
{
    [Fact]
    public void AddInternalServiceAuthentication_ShouldPropagateCorrelationHeader()
    {
        using var scope = CorrelationContext.BeginScope("corr-123");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/internal");

        request.AddInternalServiceAuthentication("management-service", "secret-token");

        Assert.Equal("management-service", Assert.Single(request.Headers.GetValues(InternalAuthHeaderNames.CallerService)));
        Assert.Equal("secret-token", Assert.Single(request.Headers.GetValues(InternalAuthHeaderNames.Credential)));
        Assert.Equal("corr-123", Assert.Single(request.Headers.GetValues(CorrelationHeaderNames.CorrelationId)));
    }

    [Fact]
    public void AddInternalServiceAuthentication_ShouldPreferExplicitCorrelationHeader()
    {
        using var scope = CorrelationContext.BeginScope("corr-from-scope");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/internal");

        request.AddInternalServiceAuthentication("management-service", "secret-token", "corr-explicit");

        Assert.Equal("corr-explicit", Assert.Single(request.Headers.GetValues(CorrelationHeaderNames.CorrelationId)));
    }
}
