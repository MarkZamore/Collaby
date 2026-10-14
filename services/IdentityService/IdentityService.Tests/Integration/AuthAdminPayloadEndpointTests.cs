using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class AuthAdminPayloadEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private static readonly Guid AuthenticatedUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly HttpClient _client;

    public AuthAdminPayloadEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task Login_ShouldReturnIsSystemAdminInUserPayload()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            emailOrUsername = "founder@collaby.test",
            password = "Secret123!"
        });

        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("user").GetProperty("isSystemAdmin").GetBoolean());
    }

    [Fact]
    public async Task Refresh_ShouldReturnIsSystemAdminInUserPayload()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "test-refresh-token"
        });

        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("user").GetProperty("isSystemAdmin").GetBoolean());
    }

    [Fact]
    public async Task Me_ShouldReturnIsSystemAdminInUserPayload()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthenticatedUserId.ToString());

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("user").GetProperty("isSystemAdmin").GetBoolean());
    }

    [Fact]
    public async Task AccessLogin_ShouldReturnIsSystemAdminInUserPayload_ForDedicatedAdminHost()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/access-login")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Host = "admin.collaby.test";
        request.Headers.Add("Cf-Access-Authenticated-User-Email", "founder@collaby.test");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("user").GetProperty("isSystemAdmin").GetBoolean());
    }

    [Fact]
    public async Task AccessLogin_ShouldHonorForwardedHost_ForDedicatedAdminHost()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/access-login")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Host = "api-identity";
        request.Headers.Add("X-Forwarded-Host", "admin.collaby.test");
        request.Headers.Add("Cf-Access-Authenticated-User-Email", "founder@collaby.test");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response);

        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.GetProperty("user").GetProperty("isSystemAdmin").GetBoolean());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }
}
