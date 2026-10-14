using System.Text.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class AuthHealthEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;

    public AuthHealthEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task Live_ShouldReturnAliveStatus()
    {
        var response = await _client.GetAsync("/api/auth/health/live");

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.Equal("alive", document.RootElement.GetProperty("status").GetString());
        Assert.Equal("identity", document.RootElement.GetProperty("service").GetString());
    }
}
