using System.Net;
using System.Net.Http.Json;
using AnalyticsService.Application.Contracts;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Configuration;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using Microsoft.Extensions.Options;

namespace AnalyticsService.Application.Services;

public sealed class ManagementProjectAnalyticsSnapshotClient : IManagementProjectAnalyticsSnapshotClient
{
    private readonly HttpClient _httpClient;
    private readonly ManagementServiceOptions _options;
    private readonly ILogger<ManagementProjectAnalyticsSnapshotClient> _logger;

    public ManagementProjectAnalyticsSnapshotClient(
        HttpClient httpClient,
        IOptions<ManagementServiceOptions> options,
        ILogger<ManagementProjectAnalyticsSnapshotClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ManagementProjectAnalyticsSnapshot?> GetProjectAnalyticsSnapshotAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/management/internal/v1/queries/projects/{projectId}/analytics-snapshot?userId={userId}");
        request.MarkAsRetryableIdempotentInternalQuery();
        request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);

        using var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Management analytics snapshot query failed for project {ProjectId}, user {UserId} with status {StatusCode}",
                projectId,
                userId,
                response.StatusCode);
            throw new InvalidOperationException(
                $"Management analytics snapshot query failed with status code {response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<ManagementProjectAnalyticsSnapshot>(
            cancellationToken: cancellationToken);

        if (payload is null)
        {
            throw new InvalidOperationException("Management analytics snapshot query returned an empty payload.");
        }

        return payload;
    }
}
