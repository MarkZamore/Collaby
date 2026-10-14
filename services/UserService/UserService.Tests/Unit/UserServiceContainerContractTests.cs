using Xunit;

namespace UserService.Tests.Unit;

public sealed class UserServiceContainerContractTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void AccessHost_ShouldExposeKubernetesReadiness()
    {
        var host = File.ReadAllText(Path.Combine(RepoRoot, "services", "AccessHost", "Program.cs"));
        var renderer = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "render-local.py"));
        Assert.Contains("/health/ready", host);
        Assert.Contains("readinessProbe", renderer);
        Assert.Contains("startupProbe", renderer);
        Assert.Contains("httpGet", renderer);
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

        throw new InvalidOperationException("Failed to resolve repository root for UserService container contract tests.");
    }
}
