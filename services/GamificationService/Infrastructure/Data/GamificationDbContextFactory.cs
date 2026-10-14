using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GamificationService.Infrastructure.Data;

public sealed class GamificationDbContextFactory : IDesignTimeDbContextFactory<GamificationDbContext>
{
    public GamificationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var servicePath = File.Exists(Path.Combine(basePath, "appsettings.json"))
            ? basePath
            : Path.Combine(basePath, "services", "GamificationService");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(servicePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Port=5432;Database=collaby_gamification;Username=postgres;Password=postgres;";

        var optionsBuilder = new DbContextOptionsBuilder<GamificationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new GamificationDbContext(optionsBuilder.Options);
    }
}
