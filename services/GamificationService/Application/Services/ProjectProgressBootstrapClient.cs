using System.Net.Http.Json;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using GamificationService.Application.Interfaces;
using GamificationService.Configuration;
using Microsoft.Extensions.Options;

namespace GamificationService.Application.Services;

public sealed class ProjectProgressBootstrapClient : IProjectProgressBootstrapClient
{
    private readonly HttpClient _httpClient;
    private readonly ManagementServiceOptions _options;
    private readonly ILogger<ProjectProgressBootstrapClient> _logger;

    public ProjectProgressBootstrapClient(
        HttpClient httpClient,
        IOptions<ManagementServiceOptions> options,
        ILogger<ProjectProgressBootstrapClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProjectProgressExportPage> ExportAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/management/internal/v1/queries/projects/progress/export?limit={limit}";
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            requestUri += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.MarkAsRetryableIdempotentInternalQuery();
        request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);

        using var response = await _httpClient.SendWithIdempotentQueryResilienceAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Project progress export failed with status code {StatusCode}", response.StatusCode);
            throw new InvalidOperationException($"Project progress export failed with status code {response.StatusCode}.");
        }

        var page = await response.Content.ReadFromJsonAsync<ProjectProgressExportPageDto>(cancellationToken: cancellationToken);
        if (page is null)
        {
            throw new InvalidOperationException("Project progress export returned an empty payload.");
        }

        return new ProjectProgressExportPage(page.Items ?? [], page.NextCursor);
    }

    private sealed class ProjectProgressExportPageDto
    {
        public List<ManagementProjectProgressSnapshotEventV1>? Items { get; set; }
        public string? NextCursor { get; set; }
    }
}
