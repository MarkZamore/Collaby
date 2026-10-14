using System.Net.Http.Headers;
using System.Net.Http.Json;
using MessengerService.Contracts.Responses;
using Xunit;

namespace MessengerService.Tests.Integration;

public sealed class PresenceControllerTests : IClassFixture<MessengerServiceTestServer>
{
    private readonly HttpClient _client;

    public PresenceControllerTests(MessengerServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task GetPresenceBatch_ShouldReturnPresenceForRequestedUsers()
    {
        var currentUserId = Guid.NewGuid();
        var requestedUserIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", currentUserId.ToString());

        var response = await _client.PostAsJsonAsync("/api/messenger/presence/batch", requestedUserIds);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<List<PresenceResponse>>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Count);
        Assert.Equal(requestedUserIds[0], payload[0].UserId);
        Assert.True(payload[0].IsOnline);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
