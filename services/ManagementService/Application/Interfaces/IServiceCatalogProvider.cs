namespace ManagementService.Application.Interfaces;

public interface IServiceCatalogProvider
{
    IReadOnlyCollection<ServiceCatalogEntry> GetEntries();
    ServiceCatalogEntry? GetRestartEligibleApplication(string composeService);
}

public sealed class ServiceCatalogEntry
{
    public string ComposeService { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Profiles { get; init; } = [];
    public string? DirectReadinessUrl { get; init; }
    public string? GatewaySmokeUrl { get; init; }
    public IReadOnlyCollection<string> OwnedStateUnits { get; init; } = [];
}
