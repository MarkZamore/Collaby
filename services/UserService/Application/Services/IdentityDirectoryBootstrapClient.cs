using System.Net.Http.Json;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.InternalQueries;
using Microsoft.Extensions.Options;
using UserService.Application.Interfaces;
using UserService.Configuration;

namespace UserService.Application.Services;

public sealed class IdentityDirectoryBootstrapClient : IIdentityDirectoryBootstrapClient
{
    private readonly HttpClient _httpClient;
    private readonly IdentityServiceOptions _options;
    private readonly ILogger<IdentityDirectoryBootstrapClient> _logger;

    public IdentityDirectoryBootstrapClient(
        HttpClient httpClient,
        IOptions<IdentityServiceOptions> options,
        ILogger<IdentityDirectoryBootstrapClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IdentityDirectoryExportPage> ExportAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var requestUri = $"/api/auth/internal/v1/queries/users/directory/export?limit={limit}";
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
            _logger.LogWarning("Identity directory export failed with status code {StatusCode}", response.StatusCode);
            throw new InvalidOperationException($"Identity directory export failed with status code {response.StatusCode}.");
        }

        var page = await response.Content.ReadFromJsonAsync<IdentityDirectoryExportPageDto>(cancellationToken: cancellationToken);
        if (page is null)
        {
            throw new InvalidOperationException("Identity directory export returned an empty payload.");
        }

        return new IdentityDirectoryExportPage(page.Items ?? [], page.NextCursor);
    }

    private sealed class IdentityDirectoryExportPageDto
    {
        public List<IdentityUserDirectoryEventV1>? Items { get; set; }
        public string? NextCursor { get; set; }
    }
}
