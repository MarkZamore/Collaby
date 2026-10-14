using System.Net.Http.Json;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using CallService.Application.Interfaces;
using CallService.Configuration;
using Microsoft.Extensions.Options;

namespace CallService.Infrastructure.Clients;

public class UserLookupClient : IUserLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly UserServiceOptions _options;
    private readonly ILogger<UserLookupClient> _logger;

    public UserLookupClient(HttpClient httpClient, IOptions<UserServiceOptions> options, ILogger<UserLookupClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, UserLookupResult>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/internal/v1/queries/users/batch")
            {
                Content = JsonContent.Create(userIds)
            };

            request.MarkAsRetryableIdempotentInternalQuery();
            request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);

            var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UserService batch lookup failed with status {StatusCode}", response.StatusCode);
                return new Dictionary<Guid, UserLookupResult>();
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(cancellationToken: ct);
            if (users == null) return new Dictionary<Guid, UserLookupResult>();

            return users.ToDictionary(u => u.UserId, u => new UserLookupResult
            {
                UserId = u.UserId,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch users from UserService");
            return new Dictionary<Guid, UserLookupResult>();
        }
    }

    private sealed class UserLookupDto
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
