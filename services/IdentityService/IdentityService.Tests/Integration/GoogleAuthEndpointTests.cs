using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class GoogleAuthEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;
    private readonly IdentityServiceTestServer.StubAuthService _authService;

    public GoogleAuthEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
        _authService = server.AuthService;
        _authService.Reset();
    }

    [Fact]
    public async Task GoogleConfig_ShouldReturnEnabledPayload()
    {
        _authService.SetGoogleConfig(enabled: true, clientId: "test-google-client-id");

        using var response = await _client.GetAsync("/api/auth/google/config");
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("test-google-client-id", document.RootElement.GetProperty("clientId").GetString());
    }

    [Fact]
    public async Task GoogleConfig_ShouldReturnDisabledPayload_WhenClientIdIsMissing()
    {
        _authService.SetGoogleConfig(enabled: false, clientId: null);

        using var response = await _client.GetAsync("/api/auth/google/config");
        response.EnsureSuccessStatusCode();

        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.False(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.True(document.RootElement.GetProperty("clientId").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);
    }

    [Fact]
    public async Task Google_ShouldReturnAuthenticatedPayload_WhenServiceSignsUserInImmediately()
    {
        _authService.SetGoogleAuthHandler(_ => Task.FromResult(new IdentityService.Contracts.Responses.GoogleAuthStartResponse
        {
            Success = true,
            AccessToken = "google-access-token",
            RefreshToken = "google-refresh-token",
            User = new IdentityService.Contracts.Responses.UserDto
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Email = "member@collaby.test",
                Username = "member"
            }
        }));

        using var response = await _client.PostAsJsonAsync("/api/auth/google", new
        {
            idToken = "google-id-token"
        });

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.False(document.RootElement.GetProperty("requiresNickname").GetBoolean());
        Assert.Equal("google-access-token", document.RootElement.GetProperty("accessToken").GetString());
        Assert.Equal("Google login successful", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Google_ShouldReturnNicknameChallenge_WhenRegistrationNeedsCompletion()
    {
        _authService.SetGoogleAuthHandler(_ => Task.FromResult(new IdentityService.Contracts.Responses.GoogleAuthStartResponse
        {
            Success = true,
            RequiresNickname = true,
            RegistrationToken = "pending-google-registration",
            SuggestedNickname = "collaby_user",
            Profile = new IdentityService.Contracts.Responses.GoogleAuthProfileResponse
            {
                Email = "new-user@collaby.test",
                Name = "New User",
                Picture = "https://example.test/picture.png"
            }
        }));

        using var response = await _client.PostAsJsonAsync("/api/auth/google", new
        {
            idToken = "google-id-token"
        });

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("requiresNickname").GetBoolean());
        Assert.Equal("pending-google-registration", document.RootElement.GetProperty("registrationToken").GetString());
        Assert.Equal("collaby_user", document.RootElement.GetProperty("suggestedNickname").GetString());
        Assert.Equal("new-user@collaby.test", document.RootElement.GetProperty("profile").GetProperty("email").GetString());
    }

    [Fact]
    public async Task GoogleRegister_ShouldReturnBadRequest_WhenCompletionFails()
    {
        _authService.SetCompleteGoogleRegistrationHandler(_ => Task.FromResult(new IdentityService.Contracts.Responses.AuthResponse
        {
            Success = false,
            Code = "nickname_taken",
            Message = "Nickname already taken"
        }));

        using var response = await _client.PostAsJsonAsync("/api/auth/google/register", new
        {
            registrationToken = "pending-google-registration",
            nickname = "taken_name"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("nickname_taken", document.RootElement.GetProperty("code").GetString());
    }
}
