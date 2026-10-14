using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class InternalUsersDirectoryExportEndpointTests : IClassFixture<IdentityServiceTestServer>
{
    private readonly HttpClient _client;

    public InternalUsersDirectoryExportEndpointTests(IdentityServiceTestServer server)
    {
        _client = server.Client;
    }

    [Fact]
    public async Task DirectoryExport_ShouldReturnVersionedSnapshotPage()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/internal/v1/queries/users/directory/export?limit=10");
        request.Headers.Add("X-Internal-Service", "user-service");
        request.Headers.Add("X-Internal-Service-Key", "test-user-key");

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DirectoryExportResponse>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Items);
        Assert.Contains(payload.Items, item => item.Email == "founder@collaby.test" && item.IsSystemAdmin);
        Assert.All(payload.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.LifecycleState)));
    }

    private sealed class DirectoryExportResponse
    {
        public List<DirectoryExportItem> Items { get; set; } = [];
        public string? NextCursor { get; set; }
    }

    private sealed class DirectoryExportItem
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSystemAdmin { get; set; }
        public string LifecycleState { get; set; } = string.Empty;
    }
}
