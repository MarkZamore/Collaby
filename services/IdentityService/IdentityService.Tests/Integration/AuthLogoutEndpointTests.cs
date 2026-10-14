using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class AuthLogoutEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;

    public AuthLogoutEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task Logout_WithoutAccessTokenOrRefreshToken_ShouldStillSucceed()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/logout", new { });

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Logged out successfully", document.RootElement.GetProperty("message").GetString());
    }
}
