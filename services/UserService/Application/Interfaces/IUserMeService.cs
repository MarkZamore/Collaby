using UserService.Contracts.Requests;
using UserService.Contracts.Responses;

namespace UserService.Application.Interfaces;

public interface IUserMeService
{
    Task<UserMeDto> GetMeAsync(Guid userId, string? usernameFallback, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetProfileAsync(Guid userId, string? usernameFallback, CancellationToken cancellationToken = default);
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, string? usernameFallback, CancellationToken cancellationToken = default);

    Task<UserPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default);

    Task<UserOnboardingDto> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserOnboardingDto> UpdateOnboardingAsync(Guid userId, UpdateOnboardingRequest request, CancellationToken cancellationToken = default);
    Task<UserOnboardingDto> CompleteOnboardingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserActivityDto> PingActivityAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserActivityDto> UpdateActivityAsync(Guid userId, UpdateActivityRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicUserDto>> GetPublicProfilesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicUserDto>> SearchUsersAsync(string query, int limit, CancellationToken cancellationToken = default);
}

