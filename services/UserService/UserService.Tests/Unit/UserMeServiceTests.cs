using Microsoft.EntityFrameworkCore;
using UserService.Application.Services;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.Repositories;
using Xunit;

namespace UserService.Tests.Unit;

public class UserMeServiceTests
{
    [Fact]
    public async Task GetMeAsync_ShouldAutoProvisionDefaultRecords()
    {
        await using var dbContext = CreateDbContext();
        var repository = new UserRepository(dbContext);
        var service = new UserMeService(repository, dbContext);
        var userId = Guid.NewGuid();

        var result = await service.GetMeAsync(userId, "johnny");

        Assert.Equal(userId, result.UserId);
        Assert.Equal("johnny", result.Profile.DisplayName);
        Assert.Equal(UserDefaults.DefaultTheme, result.Preferences.Theme);
        Assert.Null(result.Preferences.DefaultBoardBackground);
        Assert.Equal(UserDefaults.OnboardingNotStarted, result.Onboarding.Status);
        Assert.Equal(UserDefaults.DefaultTimezone, result.Profile.Timezone);
        Assert.Equal(UserDefaults.DefaultLanguage, result.Profile.Language);

        Assert.Equal(1, await dbContext.UserProfiles.CountAsync());
        Assert.Equal(1, await dbContext.UserPreferences.CountAsync());
        Assert.Equal(1, await dbContext.UserOnboarding.CountAsync());
        Assert.Equal(1, await dbContext.UserActivity.CountAsync());
    }

    [Fact]
    public async Task UpdatePreferencesAsync_ShouldPersistChanges()
    {
        await using var dbContext = CreateDbContext();
        var repository = new UserRepository(dbContext);
        var service = new UserMeService(repository, dbContext);
        var userId = Guid.NewGuid();

        await service.GetMeAsync(userId, null);

        var updated = await service.UpdatePreferencesAsync(
            userId,
            new UserService.Contracts.Requests.UpdatePreferencesRequest
            {
                Theme = "dark",
                DefaultBoardBackground = new UserService.Contracts.Requests.BoardBackgroundPreferenceRequest
                {
                    Type = "image",
                    Value = "https://images.example/background.jpg"
                },
                EmailNotifications = false,
                PushNotifications = false,
                WeeklyDigest = true
            });

        Assert.Equal("dark", updated.Theme);
        Assert.NotNull(updated.DefaultBoardBackground);
        Assert.Equal("image", updated.DefaultBoardBackground!.Type);
        Assert.Equal("https://images.example/background.jpg", updated.DefaultBoardBackground.Value);
        Assert.False(updated.EmailNotifications);
        Assert.False(updated.PushNotifications);
        Assert.True(updated.WeeklyDigest);

        var stored = await dbContext.UserPreferences.SingleAsync(x => x.UserId == userId);
        Assert.Equal("dark", stored.Theme);
        Assert.Equal("{\"Type\":\"image\",\"Value\":\"https://images.example/background.jpg\"}", stored.DefaultBoardBackgroundJson);
        Assert.False(stored.EmailNotifications);
        Assert.False(stored.PushNotifications);
        Assert.True(stored.WeeklyDigest);
    }

    [Fact]
    public async Task GetMeAsync_ShouldNormalizeLegacyLanguageAndTheme()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();

        dbContext.UserProfiles.Add(new UserProfile
        {
            UserId = userId,
            DisplayName = "johnny",
            Timezone = UserDefaults.DefaultTimezone,
            Language = "es",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.UserPreferences.Add(new UserPreference
        {
            UserId = userId,
            Theme = "custom",
            EmailNotifications = true,
            PushNotifications = true,
            WeeklyDigest = false,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.UserOnboarding.Add(new UserOnboarding
        {
            UserId = userId,
            Status = UserDefaults.OnboardingNotStarted,
            CurrentStep = 0,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.UserActivity.Add(new UserActivity
        {
            UserId = userId,
            LastActiveAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var repository = new UserRepository(dbContext);
        var service = new UserMeService(repository, dbContext);

        var result = await service.GetMeAsync(userId, "johnny");

        Assert.Equal("en", result.Profile.Language);
        Assert.Equal("dark", result.Preferences.Theme);
    }

    private static UserDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new UserDbContext(options);
    }
}
