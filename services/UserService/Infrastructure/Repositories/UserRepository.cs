using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly UserDbContext _dbContext;

    public UserRepository(UserDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task<UserPreference?> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserPreferences.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task<UserOnboarding?> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserOnboarding.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public Task<UserActivity?> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserActivity.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfile>> GetProfilesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.UserProfiles
            .Where(x => userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);
    }

    public void AddProfile(UserProfile profile)
    {
        _dbContext.UserProfiles.Add(profile);
    }

    public void AddPreference(UserPreference preference)
    {
        _dbContext.UserPreferences.Add(preference);
    }

    public void AddOnboarding(UserOnboarding onboarding)
    {
        _dbContext.UserOnboarding.Add(onboarding);
    }

    public void AddActivity(UserActivity activity)
    {
        _dbContext.UserActivity.Add(activity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}

