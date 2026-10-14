using System.Text.Json;
using System.Xml.Linq;
using Xunit;
using Xunit.Sdk;

namespace ManagementService.ArchitectureTests;

public sealed class ServiceBoundaryArchitectureTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();
    private static readonly string ServiceCatalogPath = Path.Combine(RepoRoot, "platform", "contracts", "runtime", "service-catalog.json");
    private static readonly string CaddyfilePath = Path.Combine(RepoRoot, "frontend", "Caddyfile");
    private static readonly string CloudflareMainPath = Path.Combine(RepoRoot, "cloudflare", "main.tf");
    private static readonly string PgServersPath = Path.Combine(RepoRoot, "panels", "pg", "servers.json");

    private static readonly string[] RuntimeModules =
    [
        "IdentityService",
        "UserService",
        "ManagementService",
        "AnalyticsService",
        "MessengerService",
        "CallService",
        "GamificationService"
    ];

    [Fact]
    public void RuntimeModules_ShouldReferenceOnlyOwnAssemblies_And_NeutralPlatformProjects()
    {
        foreach (var module in RuntimeModules)
        {
            var projectPath = Path.Combine(RepoRoot, "services", module, $"{module}.csproj");
            var project = XDocument.Load(projectPath);

            var references = project.Descendants("ProjectReference")
                .Select(element => (string?)element.Attribute("Include"))
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => NormalizePath(reference!))
                .ToArray();

            foreach (var reference in references)
            {
                Assert.True(
                    reference.EndsWith("platform/dotnet/Collaby.EventBus/Collaby.EventBus.csproj", StringComparison.OrdinalIgnoreCase) ||
                    reference.EndsWith("platform/dotnet/Collaby.ServiceDefaults/Collaby.ServiceDefaults.csproj", StringComparison.OrdinalIgnoreCase) ||
                    reference.EndsWith("platform/dotnet/Collaby.InternalContracts/Collaby.InternalContracts.csproj", StringComparison.OrdinalIgnoreCase),
                    $"{module} must not reference another runtime module directly. Found {reference}.");
            }
        }
    }

    [Fact]
    public void SharedPlatformProjects_ShouldStayServiceAgnostic()
    {
        var platformRoots = new[]
        {
            Path.Combine(RepoRoot, "platform", "dotnet", "Collaby.EventBus"),
            Path.Combine(RepoRoot, "platform", "dotnet", "Collaby.ServiceDefaults"),
            Path.Combine(RepoRoot, "platform", "dotnet", "Collaby.InternalContracts")
        };

        foreach (var platformRoot in platformRoots)
        {
            foreach (var filePath in Directory.EnumerateFiles(platformRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = File.ReadAllText(filePath);
                Assert.DoesNotContain("using IdentityService.", content);
                Assert.DoesNotContain("using UserService.", content);
                Assert.DoesNotContain("using ManagementService.", content);
                Assert.DoesNotContain("using AnalyticsService.", content);
                Assert.DoesNotContain("using MessengerService.", content);
                Assert.DoesNotContain("using CallService.", content);
                Assert.DoesNotContain("using GamificationService.", content);
            }
        }
    }

    [Fact]
    public void Solution_ShouldInclude_CombinedHostProjects()
    {
        var solution = File.ReadAllText(Path.Combine(RepoRoot, "Collaby.slnx"));

        Assert.Contains("services/AccessHost/AccessHost.csproj", solution);
        Assert.Contains("services/WorkspaceHost/WorkspaceHost.csproj", solution);
        Assert.Contains("services/RealtimeHost/RealtimeHost.csproj", solution);
    }

    [Fact]
    public void KubernetesRuntime_ShouldPreserveServicesAndStorageIsolation()
    {
        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot, "kubernetes", "runtime-spec.json")));
        foreach (var name in new[] { "access", "workspace", "realtime", "engagement", "assistant", "frontend", "postgres", "mongo-db", "cache", "events", "alloy", "mailpit" })
            Assert.True(spec.RootElement.TryGetProperty(name, out _), name);
        Assert.False(spec.RootElement.TryGetProperty("gitlab", out _));
        Assert.False(spec.RootElement.TryGetProperty("promtail", out _));
        var postgres = spec.RootElement.GetProperty("postgres").GetProperty("environment");
        foreach (var module in new[] { "IDENTITY", "USERS", "MANAGEMENT", "ANALYTICS", "GAMIFICATION" })
            Assert.True(postgres.TryGetProperty($"POSTGRES_{module}_PASSWORD", out _));
    }

    [Fact]
    public void ServiceCatalog_ShouldBeIndependentOfAnsible()
    {
        var path = Path.Combine(RepoRoot, "platform", "contracts", "runtime", "service-catalog.json");
        using var catalog = JsonDocument.Parse(File.ReadAllText(path));
        var apps = catalog.RootElement.GetProperty("services").EnumerateArray().Where(s => s.GetProperty("kind").GetString() == "application").Select(s => s.GetProperty("composeService").GetString()).Order().ToArray();
        Assert.Equal(new[] { "access", "assistant", "engagement", "realtime", "workspace" }, apps);
        foreach (var p in Directory.EnumerateFiles(Path.Combine(RepoRoot,"services"), "*.csproj", SearchOption.AllDirectories))
            Assert.DoesNotContain("ansible\\roles", File.ReadAllText(p));
    }

    [Fact]
    public void Runtime_ShouldUseBoundedRestartRightsAndAnsibleJob()
    {
        var bootstrap = File.ReadAllText(Path.Combine(RepoRoot,"kubernetes","bootstrap.yml"));
        Assert.Contains("resourceNames:", bootstrap);
        foreach (var target in new[] { "access", "workspace", "realtime", "engagement", "assistant" }) Assert.Contains("- " + target, bootstrap);
        Assert.Contains("- patch", bootstrap);
        var playbook = File.ReadAllText(Path.Combine(RepoRoot,"ansible","local.yml"));
        Assert.Contains("kubernetes.core.k8s", playbook);
        Assert.DoesNotContain("ansible.builtin.shell", playbook);
    }

    [Fact]
    public void GitHubChecks_ShouldNotPublishOrDeploy()
    {
        var ci = File.ReadAllText(Path.Combine(RepoRoot,".github","workflows","ci.yml"));
        Assert.Contains("pull_request:", ci);
        Assert.Contains("workflow_dispatch:", ci);
        Assert.Contains("push: false", ci);
        Assert.DoesNotContain("git push", ci);
        Assert.DoesNotContain("deploy-launch", ci);
        Assert.False(File.Exists(Path.Combine(RepoRoot,".gitlab-ci.yml")));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null) {
            if (File.Exists(Path.Combine(directory.FullName,"Collaby.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Не найден корень Collaby.");
    }
}
