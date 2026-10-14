using System.Net.Http.Headers;
using System.Net.Http.Json;
using CallService.Contracts.Responses;
using Xunit;

namespace CallService.Tests.Integration;

public sealed class CallsControllerTests : IClassFixture<CallServiceTestServer>
{
    private readonly HttpClient _client;

    public CallsControllerTests(CallServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task GetActiveCall_ShouldReturnCurrentUsersActiveCall()
    {
        var userId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId.ToString());

        var response = await _client.GetAsync("/api/calls/active");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CallSessionResponse>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal("active-call", payload!.Id);
        Assert.Equal(userId, payload.InitiatorId);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
