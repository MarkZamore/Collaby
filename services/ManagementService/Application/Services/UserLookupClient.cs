using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using Microsoft.Extensions.Options;
using ManagementService.Application.Interfaces;
using ManagementService.Configuration;

namespace ManagementService.Application.Services;

/// <summary>
/// HTTP-клиент для внутренних service-to-service вызовов в UserService.
/// Работает поверх текущего internal auth-контракта.
/// </summary>
public sealed class UserLookupClient : IUserLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly UserServiceOptions _options;
    private readonly ILogger<UserLookupClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UserLookupClient(
        HttpClient httpClient,
        IOptions<UserServiceOptions> options,
        ILogger<UserLookupClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserLookupResult>();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/internal/v1/queries/users/batch")
        {
            Content = JsonContent.Create(userIds)
        };

        request.MarkAsRetryableIdempotentInternalQuery();
        AddInternalApiHeader(request);

        using var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "UserService batch lookup failed with status {StatusCode} for {Count} user IDs",
                response.StatusCode, userIds.Count);
            return new Dictionary<Guid, UserLookupResult>();
        }

        var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(_jsonOptions, cancellationToken);
        if (users is null)
            return new Dictionary<Guid, UserLookupResult>();

        return users.ToDictionary(
            u => u.UserId,
            u => new UserLookupResult
            {
                UserId = u.UserId,
                Username = u.Username,
                Email = u.Email,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            });
    }

    public async Task<IReadOnlyList<UserLookupResult>> SearchUsersAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var uri = $"/api/users/internal/v1/queries/users/search?q={Uri.EscapeDataString(query.Trim())}&limit={limit}";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.MarkAsRetryableIdempotentInternalQuery();
        AddInternalApiHeader(request);

        using var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "UserService search request failed with status {StatusCode} for query '{Query}'",
                response.StatusCode, query);
            return [];
        }

        var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(_jsonOptions, cancellationToken);
        if (users is null)
            return [];

        return users.Select(u => new UserLookupResult
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            DisplayName = u.DisplayName,
            AvatarUrl = u.AvatarUrl
        }).ToList();
    }

    private void AddInternalApiHeader(HttpRequestMessage request)
    {
        request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);
    }

    private sealed class UserLookupDto
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
