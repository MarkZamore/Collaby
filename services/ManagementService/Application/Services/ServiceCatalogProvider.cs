using System.Text.Json;
using ManagementService.Application.Interfaces;

namespace ManagementService.Application.Services;

public sealed class ServiceCatalogProvider : IServiceCatalogProvider
{
    private readonly IReadOnlyCollection<ServiceCatalogEntry> _entries;

    public ServiceCatalogProvider()
    {
        var catalogPath = Path.Combine(AppContext.BaseDirectory, "service-catalog.json");
        if (!File.Exists(catalogPath))
        {
            throw new InvalidOperationException($"Service catalog is missing at '{catalogPath}'.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        _entries = document.RootElement.GetProperty("services")
            .EnumerateArray()
            .Select(entry => new ServiceCatalogEntry
            {
                ComposeService = entry.GetProperty("composeService").GetString() ?? string.Empty,
                Kind = entry.GetProperty("kind").GetString() ?? string.Empty,
                Profiles = entry.TryGetProperty("profiles", out var profilesElement)
                    ? profilesElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : [],
                DirectReadinessUrl = entry.TryGetProperty("directReadinessUrl", out var directElement) ? directElement.GetString() : null,
                GatewaySmokeUrl = entry.TryGetProperty("gatewaySmokeUrl", out var gatewayElement) ? gatewayElement.GetString() : null,
                OwnedStateUnits = entry.TryGetProperty("ownedStateUnits", out var stateUnitsElement)
                    ? stateUnitsElement.EnumerateArray().Select(value => value.GetString() ?? string.Empty).Where(value => value.Length > 0).ToArray()
                    : []
            })
            .ToArray();
    }

    public IReadOnlyCollection<ServiceCatalogEntry> GetEntries()
    {
        return _entries;
    }

    public ServiceCatalogEntry? GetRestartEligibleApplication(string composeService)
    {
        return _entries.FirstOrDefault(entry =>
            string.Equals(entry.ComposeService, composeService, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(entry.Kind, "application", StringComparison.OrdinalIgnoreCase));
    }
}
