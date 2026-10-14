using System.Net.Http.Json;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using GamificationService.Application.Interfaces;
using GamificationService.Configuration;
using Microsoft.Extensions.Options;

namespace GamificationService.Application.Services;

public sealed class ProjectAuthorizationClient : IProjectAuthorizationClient
{
    private readonly HttpClient _httpClient;
    private readonly ManagementServiceOptions _options;
    private readonly ILogger<ProjectAuthorizationClient> _logger;

    public ProjectAuthorizationClient(
        HttpClient httpClient,
        IOptions<ManagementServiceOptions> options,
        ILogger<ProjectAuthorizationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProjectAuthorizationResult> GetProjectAuthorizationAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/management/internal/v1/queries/projects/{projectId}/memberships/{userId}/owner-or-admin");
        request.MarkAsRetryableIdempotentInternalQuery();
        request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);

        using var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Project authorization query failed for project {ProjectId}, user {UserId} with status code {StatusCode}",
                projectId,
                userId,
                response.StatusCode);
            throw new InvalidOperationException($"Project authorization query failed with status code {response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<ProjectAuthorizationDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Project authorization query returned an empty payload.");
        }

        return new ProjectAuthorizationResult(payload.ProjectId, payload.WorkspaceId, payload.IsOwnerOrAdmin);
    }

    private sealed class ProjectAuthorizationDto
    {
        public Guid ProjectId { get; set; }
        public Guid WorkspaceId { get; set; }
        public bool IsOwnerOrAdmin { get; set; }
    }
}
