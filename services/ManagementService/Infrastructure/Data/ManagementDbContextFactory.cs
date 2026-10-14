using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ManagementService.Infrastructure.Data;

public sealed class ManagementDbContextFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    public ManagementDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ManagementDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=collaby_management;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
        return new ManagementDbContext(optionsBuilder.Options);
    }
}
