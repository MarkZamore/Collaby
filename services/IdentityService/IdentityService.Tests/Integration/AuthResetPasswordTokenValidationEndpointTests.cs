using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityService.Contracts.Responses;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class AuthResetPasswordTokenValidationEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;
    private readonly IdentityServiceTestServer.StubAuthService _authService;

    public AuthResetPasswordTokenValidationEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
        _authService = server.AuthService;
        _authService.Reset();
    }

    [Fact]
    public async Task ValidateResetPasswordToken_ShouldReturnSuccess_WhenTokenIsValid()
    {
        using var response = await _client.PostAsJsonAsync("/api/auth/validate-reset-password-token", new
        {
            token = "valid-reset-token",
            email = "member@collaby.test",
        });

        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("reset_token_valid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ValidateResetPasswordToken_ShouldReturnBadRequest_WhenServiceRejectsToken()
    {
        _authService.SetValidateResetPasswordTokenHandler(_ => Task.FromResult(new AuthResponse
        {
            Success = false,
            Code = "invalid_reset_token",
            Message = "Invalid or expired reset token",
        }));

        using var response = await _client.PostAsJsonAsync("/api/auth/validate-reset-password-token", new
        {
            token = "used-reset-token",
            email = "member@collaby.test",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var body = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(body);

        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_reset_token", document.RootElement.GetProperty("code").GetString());
    }
}
