using System.Net.Http.Json;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using GamificationService.Application.Interfaces;
using GamificationService.Configuration;
using Microsoft.Extensions.Options;

namespace GamificationService.Application.Services;

public sealed class UserLookupClient : IUserLookupClient
{
    private readonly HttpClient _httpClient;
    private readonly UserServiceOptions _options;
    private readonly ILogger<UserLookupClient> _logger;

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
        {
            return new Dictionary<Guid, UserLookupResult>();
        }

        try
        {
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
                    "UserService batch lookup failed with status {StatusCode}",
                    response.StatusCode);
                return new Dictionary<Guid, UserLookupResult>();
            }

            var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(cancellationToken: cancellationToken);
            if (users is null)
            {
                return new Dictionary<Guid, UserLookupResult>();
            }

            return users.ToDictionary(
                user => user.UserId,
                user => new UserLookupResult
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch leaderboard user enrichment data from UserService");
            return new Dictionary<Guid, UserLookupResult>();
        }
    }

    private void AddInternalApiHeader(HttpRequestMessage request)
    {
        request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);
    }

    private sealed class UserLookupDto
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
