using GamificationService.Application.Interfaces;
using GamificationService.Application.Services;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class LeaderboardServiceTests
{
    [Fact]
    public async Task GetGlobalTopAsync_WhenUserLookupFails_ShouldReturnLeaderboardWithoutProfileFields()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();

        dbContext.UserGlobalXp.Add(new UserGlobalXp
        {
            UserId = userId,
            TotalXp = 320,
            Level = 3,
            Coins = 25
        });

        await dbContext.SaveChangesAsync();

        var service = new LeaderboardService(
            dbContext,
            new ServiceCollection().BuildServiceProvider(),
            new ThrowingUserLookupClient(),
            NullLogger<LeaderboardService>.Instance);

        var entries = (await service.GetGlobalTopAsync()).ToList();

        var entry = Assert.Single(entries);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(320, entry.Xp);
        Assert.Null(entry.Username);
        Assert.Null(entry.AvatarUrl);
    }

    private static GamificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GamificationDbContext(options);
    }

    private sealed class ThrowingUserLookupClient : IUserLookupClient
    {
        public Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("UserService unavailable");
    }
}
