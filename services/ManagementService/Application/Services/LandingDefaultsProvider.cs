using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Landing;

namespace ManagementService.Application.Services;

public sealed class LandingDefaultsProvider : ILandingDefaultsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LandingContentDocument> GetAsync(string locale, CancellationToken cancellationToken = default)
    {
        var normalizedLocale = LandingLocales.Normalize(locale);
        var filePath = Path.Combine(AppContext.BaseDirectory, "cms-defaults", "landing", $"{normalizedLocale}.json");
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Landing fallback file for locale '{normalizedLocale}' not found.", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var content = await JsonSerializer.DeserializeAsync<LandingContentDocument>(stream, JsonOptions, cancellationToken);
        return content ?? new LandingContentDocument();
    }
}
