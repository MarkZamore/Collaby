using System.Net.Http.Headers;
using System.Net.Http.Json;
using GamificationService.Contracts.Responses;
using Xunit;

namespace GamificationService.Tests.Integration;

public sealed class XpControllerTests : IClassFixture<GamificationServiceTestServer>
{
    private readonly HttpClient _client;

    public XpControllerTests(GamificationServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task GetGlobalXp_ShouldReturnAuthenticatedUsersXp()
    {
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId.ToString());

        var response = await _client.GetAsync("/api/gamification/xp/global");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<UserXpResponse>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal(userId, payload!.UserId);
        Assert.Equal(120, payload.TotalXp);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
