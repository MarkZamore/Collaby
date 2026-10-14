using GamificationService.Application.Services;
using GamificationService.Domain.Constants;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class StreakServiceTests
{
    [Fact]
    public async Task UseFreezeAsync_ShouldConsumeFreezeItemByStableId()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new GamificationDbContext(options);
        var userId = Guid.NewGuid();

        dbContext.UserGlobalXp.Add(new UserGlobalXp
        {
            UserId = userId,
            FreezeCount = 0
        });

        dbContext.ShopItems.Add(new ShopItem
        {
            Id = GamificationCatalog.StreakFreezeItemId,
            Name = "Series Freeze",
            Category = "consumables",
            Price = 500
        });

        dbContext.UserInventory.Add(new UserInventory
        {
            UserId = userId,
            ItemId = GamificationCatalog.StreakFreezeItemId
        });

        await dbContext.SaveChangesAsync();

        var service = new StreakService(dbContext);
        var applied = await service.UseFreezeAsync(userId);

        Assert.True(applied);
        Assert.Equal(1, (await dbContext.UserGlobalXp.SingleAsync(x => x.UserId == userId)).FreezeCount);
        Assert.False(await dbContext.UserInventory.AnyAsync(x => x.UserId == userId && x.ItemId == GamificationCatalog.StreakFreezeItemId));
    }
}
