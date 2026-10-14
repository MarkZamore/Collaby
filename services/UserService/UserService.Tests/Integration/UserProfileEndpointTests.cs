using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using UserService.Contracts.Responses;
using Xunit;

namespace UserService.Tests.Integration;

public class UserProfileEndpointTests : IClassFixture<UserServiceTestServer>
{
    private readonly UserServiceTestServer _server;

    public UserProfileEndpointTests(UserServiceTestServer server)
    {
        _server = server;
    }

    [Fact]
    public async Task GetProfile_ShouldReturnUnauthorized_WithoutToken()
    {
        var response = await _server.Client.GetAsync("/api/users/me/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_ShouldReturnUserProfile_ForAuthorizedUser()
    {
        var userId = Guid.NewGuid();

        _server.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", userId.ToString());

        var response = await _server.Client.GetAsync("/api/users/me/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<UserProfileDto>(new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.Equal(userId, payload!.UserId);

        _server.Client.DefaultRequestHeaders.Authorization = null;
    }
}

