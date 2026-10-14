using System.Text.Json;
using IdentityService.Application.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class ConfigurationUserDeletionTargetCatalogTests
{
    private static readonly object CatalogLock = new();

    [Fact]
    public void Catalog_ShouldExpose_ModuleTargets_From_ContainedModules()
    {
        lock (CatalogLock)
        {
            var catalogPath = Path.Combine(AppContext.BaseDirectory, "service-catalog.json");
            var previousContent = File.Exists(catalogPath) ? File.ReadAllText(catalogPath) : null;

            try
            {
                var catalog = new
                {
                    services = new object[]
                    {
                        CreateApplication("access", "identity-service", "user-service"),
                        CreateApplication("workspace", "management-service", "analytics-service"),
                        CreateApplication("realtime", "messenger-service", "call-service"),
                        CreateApplication("engagement", "gamification-service"),
                        CreateApplication("assistant", "ai-agent")
                    }
                };

                File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog), System.Text.Encoding.UTF8);

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["AnalyticsService:Enabled"] = "true"
                    })
                    .Build();

                var targetCatalog = new ConfigurationUserDeletionTargetCatalog(configuration);
                var enabledTargets = targetCatalog.GetEnabledTargets().Select(target => target.ServiceName).ToArray();

                Assert.Contains("user-service", enabledTargets);
                Assert.Contains("management-service", enabledTargets);
                Assert.Contains("messenger-service", enabledTargets);
                Assert.Contains("call-service", enabledTargets);
                Assert.Contains("gamification-service", enabledTargets);
                Assert.Contains("analytics-service", enabledTargets);
                Assert.NotNull(targetCatalog.Find("management-service"));
                Assert.NotNull(targetCatalog.Find("analytics-service"));
            }
            finally
            {
                if (previousContent is null)
                {
                    File.Delete(catalogPath);
                }
                else
                {
                    File.WriteAllText(catalogPath, previousContent, System.Text.Encoding.UTF8);
                }
            }
        }
    }

    private static object CreateApplication(string composeService, params string[] modules)
    {
        return new
        {
            composeService,
            owner = composeService,
            kind = "application",
            containedModules = modules,
            eventProducers = new[] { "admin.user-deletion-results.v1" },
            eventConsumers = new[] { "admin.user-deletion-commands.v1" }
        };
    }
}
