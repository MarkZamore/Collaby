using System.Security.Claims;
using ManagementService.Application.Services;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class SystemAdministrationTests
{
    [Fact]
    public void SystemAdminEmailParser_ShouldNormalizeTrimAndDeduplicate()
    {
        var result = SystemAdminEmailParser.Parse(" Founder@Collaby.test, founder@collaby.test , ops@collaby.test ");

        Assert.Equal(2, result.Count);
        Assert.Contains("founder@collaby.test", result);
        Assert.Contains("ops@collaby.test", result);
    }

    [Fact]
    public async Task SystemAdminAuthorizationHandler_ShouldSucceedOnlyForAllowlistedEmail()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SystemAdminConstants.EmailsConfigurationKey] = "founder@collaby.test"
            })
            .Build();
        var roster = new ConfigurationSystemAdminRoster(configuration);
        var handler = new SystemAdminAuthorizationHandler(roster);
        var requirement = new SystemAdminRequirement();

        var allowedContext = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "Founder@Collaby.test")], "tests")),
            null);

        await handler.HandleAsync(allowedContext);

        Assert.True(allowedContext.HasSucceeded);

        var deniedContext = new AuthorizationHandlerContext(
            [requirement],
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "member@collaby.test")], "tests")),
            null);

        await handler.HandleAsync(deniedContext);

        Assert.False(deniedContext.HasSucceeded);
    }

    [Fact]
    public void ServiceCatalogProvider_ShouldAllowOnlyApplicationRestartTargets()
    {
        EnsureCatalogFileInTestOutput();

        var provider = new ServiceCatalogProvider();

        Assert.NotNull(provider.GetRestartEligibleApplication("access"));
        Assert.NotNull(provider.GetRestartEligibleApplication("workspace"));
        Assert.NotNull(provider.GetRestartEligibleApplication("realtime"));
        Assert.NotNull(provider.GetRestartEligibleApplication("engagement"));
        Assert.NotNull(provider.GetRestartEligibleApplication("assistant"));
        Assert.Null(provider.GetRestartEligibleApplication("frontend"));
        Assert.Null(provider.GetRestartEligibleApplication("postgres"));
        Assert.Null(provider.GetRestartEligibleApplication("grafana"));
    }

    private static void EnsureCatalogFileInTestOutput()
    {
        var outputCatalogPath = Path.Combine(AppContext.BaseDirectory, "service-catalog.json");
        if (File.Exists(outputCatalogPath))
        {
            return;
        }

        var repoRoot = ResolveRepoRoot();
        var sourceCatalogPath = Path.Combine(repoRoot, "ansible", "roles", "collaby_deploy", "service-catalog.json");
        File.Copy(sourceCatalogPath, outputCatalogPath, overwrite: true);
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Collaby.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output directory.");
    }
}
