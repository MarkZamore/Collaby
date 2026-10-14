using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace ManagementService.Tests.Integration;

public sealed class HealthControllerTests : IClassFixture<ManagementServiceTestServer>
{
    private readonly HttpClient _client;

    public HealthControllerTests(ManagementServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task GetProtected_ShouldReturnAuthenticatedUserId()
    {
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId.ToString());

        var response = await _client.GetAsync("/api/management/debug/health/protected");

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.Equal("Authenticated", document.RootElement.GetProperty("message").GetString());
        Assert.Equal(userId.ToString(), document.RootElement.GetProperty("userId").GetString());

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
