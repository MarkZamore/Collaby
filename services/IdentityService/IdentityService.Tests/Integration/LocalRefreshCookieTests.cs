using System.Net.Http.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class LocalRefreshCookieTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;
    public LocalRefreshCookieTests(IdentityServiceTestServer server) => _client = server.Client;

    [Fact]
    public async Task HttpLoginAndLogout_UseSameRootCookiePathWithoutSecure()
    {
        using var login = await _client.PostAsJsonAsync("/api/auth/login", new { emailOrUsername = "member", password = "test-password" });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("refreshToken="));
        Assert.Contains("path=/", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        refreshRequest.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var refresh = await _client.SendAsync(refreshRequest);
        refresh.EnsureSuccessStatusCode();
        using var logout = await _client.PostAsJsonAsync("/api/auth/logout", new { });
        logout.EnsureSuccessStatusCode();
        var removed = logout.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith("refreshToken="));
        Assert.Contains("path=/", removed);
        Assert.DoesNotContain("secure", removed, StringComparison.OrdinalIgnoreCase);
    }
}
