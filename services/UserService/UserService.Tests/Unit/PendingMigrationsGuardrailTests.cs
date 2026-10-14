using Microsoft.EntityFrameworkCore;
using UserService.Infrastructure.Data;
using Xunit;

namespace UserService.Tests.Unit;

public class PendingMigrationsGuardrailTests
{
    [Fact]
    public void UserDbContext_ShouldNotHavePendingModelChanges()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseNpgsql("Host=localhost;Database=collaby_users;Username=users_user;Password=placeholder")
            .Options;

        using var dbContext = new UserDbContext(options);

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }
}
