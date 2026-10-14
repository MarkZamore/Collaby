using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace IdentityService.Application.Services;

public sealed class ConfigurationUserDeletionTargetCatalog : IUserDeletionTargetCatalog
{
    private const string CatalogFileName = "service-catalog.json";
    private static readonly string[] RequiredServices =
    [
        "user-service",
        "management-service",
        "messenger-service",
        "call-service",
        "gamification-service"
    ];

    private readonly IReadOnlyList<UserDeletionTargetDefinition> _targets;

    public ConfigurationUserDeletionTargetCatalog(IConfiguration configuration)
    {
        var analyticsEnabled = configuration.GetValue("AnalyticsService:Enabled", false);
        var catalogPath = Path.Combine(AppContext.BaseDirectory, CatalogFileName);
        if (!File.Exists(catalogPath))
        {
            throw new InvalidOperationException($"{CatalogFileName} must be available for user deletion target discovery.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath));
        var availableTargets = document.RootElement.GetProperty("services")
            .EnumerateArray()
            .Where(service => string.Equals(service.GetProperty("kind").GetString(), "application", StringComparison.OrdinalIgnoreCase))
            .Where(service => HasTopic(service, "eventConsumers", "admin.user-deletion-commands.v1"))
            .Where(service => HasTopic(service, "eventProducers", "admin.user-deletion-results.v1"))
            .SelectMany(GetDeletionTargets)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var targets = new List<UserDeletionTargetDefinition>();
        foreach (var requiredService in RequiredServices)
        {
            if (!availableTargets.Contains(requiredService))
            {
                throw new InvalidOperationException($"service-catalog.json must expose deletion target '{requiredService}'.");
            }

            targets.Add(new UserDeletionTargetDefinition
            {
                ServiceName = requiredService,
                Enabled = true
            });
        }

        targets.Add(new UserDeletionTargetDefinition
        {
            ServiceName = "analytics-service",
            Enabled = analyticsEnabled && availableTargets.Contains("analytics-service")
        });

        _targets = targets;
    }

    public IReadOnlyList<UserDeletionTargetDefinition> GetEnabledTargets()
    {
        return _targets.Where(static target => target.Enabled).ToArray();
    }

    public UserDeletionTargetDefinition? Find(string serviceName)
    {
        return _targets.FirstOrDefault(target =>
            string.Equals(target.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasTopic(JsonElement service, string propertyName, string topicName)
    {
        return service.GetProperty(propertyName)
            .EnumerateArray()
            .Select(entry => entry.GetString())
            .Any(entry => string.Equals(entry, topicName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetDeletionTargets(JsonElement service)
    {
        if (service.TryGetProperty("containedModules", out var modulesElement))
        {
            var modules = modulesElement.EnumerateArray()
                .Select(entry => entry.GetString())
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .ToArray();

            if (modules.Length > 0)
            {
                return modules!;
            }
        }

        var owner = service.GetProperty("owner").GetString();
        return string.IsNullOrWhiteSpace(owner) ? [] : [owner];
    }
}
