using System.Text.Json;
using Microsoft.Extensions.Logging;
using ManagementService.Application.Interfaces;

namespace ManagementService.Application.Services;

/// <summary>
/// Поиск изображений через Unsplash API для выбора фона доски.
/// При отсутствии Unsplash:AccessKey возвращает пустой список и логирует предупреждение.
/// </summary>
public sealed class UnsplashImageSearchService : IUnsplashImageSearchService
{
    private const string HttpClientName = "Unsplash";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UnsplashImageSearchService> _logger;

    public UnsplashImageSearchService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<UnsplashImageSearchService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UnsplashImageResult>> SearchAsync(string query, int perPage = 12, int page = 1, CancellationToken cancellationToken = default)
    {
        var accessKey = _configuration["Unsplash:AccessKey"]?.Trim();
        if (string.IsNullOrEmpty(accessKey))
        {
            _logger.LogWarning("Unsplash:AccessKey is not configured. Board background image search is disabled.");
            return Array.Empty<UnsplashImageResult>();
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var pageNum = Math.Max(1, page);
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"search/photos?query={Uri.EscapeDataString(query)}&per_page={Math.Clamp(perPage, 1, 30)}&page={pageNum}");
        request.Headers.Add("Authorization", "Client-ID " + accessKey);

        try
        {
            var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Unsplash API returned {StatusCode}. Image search may be rate-limited or key invalid.", response.StatusCode);
                return Array.Empty<UnsplashImageResult>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var results = new List<UnsplashImageResult>();
            if (doc.RootElement.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in resultsEl.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    string? url = null;
                    if (item.TryGetProperty("urls", out var urlsEl))
                    {
                        if (urlsEl.TryGetProperty("regular", out var reg))
                            url = reg.GetString();
                        else if (urlsEl.TryGetProperty("full", out var full))
                            url = full.GetString();
                    }
                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(url))
                        results.Add(new UnsplashImageResult(id, url));
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unsplash image search failed for query '{Query}'.", query);
            return Array.Empty<UnsplashImageResult>();
        }
    }
}
