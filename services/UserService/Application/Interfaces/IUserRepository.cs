using UserService.Domain.Entities;

namespace UserService.Application.Interfaces;

public interface IUserRepository
{
    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserPreference?> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserOnboarding?> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserActivity?> GetActivityAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetProfilesByUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    void AddProfile(UserProfile profile);
    void AddPreference(UserPreference preference);
    void AddOnboarding(UserOnboarding onboarding);
    void AddActivity(UserActivity activity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

