using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using UserService.Application.Interfaces;
using UserService.Contracts.Requests;
using UserService.Contracts.Responses;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;

namespace UserService.Application.Services;

public class UserMeService : IUserMeService
{
    private static readonly HashSet<string> SupportedLanguages = ["en", "ru", "kk"];
    private static readonly HashSet<string> SupportedThemes = ["light", "dark", "system"];
    private static readonly HashSet<string> SupportedBoardBackgroundTypes = ["color", "image", "upload"];

    private readonly IUserRepository _userRepository;
    private readonly UserDbContext _dbContext;

    public UserMeService(IUserRepository userRepository, UserDbContext dbContext)
    {
        _userRepository = userRepository;
        _dbContext = dbContext;
    }

    public async Task<UserMeDto> GetMeAsync(Guid userId, string? usernameFallback, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, usernameFallback, cancellationToken);

        return new UserMeDto
        {
            UserId = userId,
            Profile = MapProfile(entities.Profile, usernameFallback),
            Preferences = MapPreferences(entities.Preferences),
            Onboarding = MapOnboarding(entities.Onboarding),
            Activity = MapActivity(entities.Activity)
        };
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, string? usernameFallback, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, usernameFallback, cancellationToken);
        return MapProfile(entities.Profile, usernameFallback);
    }

    public async Task<UserProfileDto> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        string? usernameFallback,
        CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, usernameFallback, cancellationToken);
        var profile = entities.Profile;

        if (request.DisplayName is not null)
        {
            profile.DisplayName = NormalizeOptionalText(request.DisplayName);
        }

        if (request.AvatarUrl is not null)
        {
            profile.AvatarUrl = NormalizeOptionalText(request.AvatarUrl);
        }

        if (request.Bio is not null)
        {
            profile.Bio = NormalizeOptionalText(request.Bio);
        }

        if (!string.IsNullOrWhiteSpace(request.Timezone))
        {
            profile.Timezone = request.Timezone.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            profile.Language = NormalizeLanguage(request.Language);
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapProfile(profile, usernameFallback);
    }

    public async Task<UserPreferencesDto> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        return MapPreferences(entities.Preferences);
    }

    public async Task<UserPreferencesDto> UpdatePreferencesAsync(Guid userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        var preferences = entities.Preferences;

        if (!string.IsNullOrWhiteSpace(request.Theme))
        {
            preferences.Theme = NormalizeTheme(request.Theme);
        }

        if (request.ResetDefaultBoardBackground)
        {
            preferences.DefaultBoardBackgroundJson = null;
        }
        else if (request.DefaultBoardBackground is not null)
        {
            preferences.DefaultBoardBackgroundJson = NormalizeBoardBackground(request.DefaultBoardBackground);
        }

        if (request.EmailNotifications.HasValue)
        {
            preferences.EmailNotifications = request.EmailNotifications.Value;
        }

        if (request.PushNotifications.HasValue)
        {
            preferences.PushNotifications = request.PushNotifications.Value;
        }

        if (request.WeeklyDigest.HasValue)
        {
            preferences.WeeklyDigest = request.WeeklyDigest.Value;
        }

        preferences.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapPreferences(preferences);
    }

    public async Task<UserOnboardingDto> GetOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        return MapOnboarding(entities.Onboarding);
    }

    public async Task<UserOnboardingDto> UpdateOnboardingAsync(Guid userId, UpdateOnboardingRequest request, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        var onboarding = entities.Onboarding;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            onboarding.Status = request.Status.Trim().ToLowerInvariant();
        }

        if (request.CurrentStep.HasValue)
        {
            onboarding.CurrentStep = request.CurrentStep.Value;
        }

        if (string.Equals(onboarding.Status, UserDefaults.OnboardingCompleted, StringComparison.OrdinalIgnoreCase))
        {
            onboarding.CompletedAt ??= DateTime.UtcNow;
        }
        else
        {
            onboarding.CompletedAt = null;
        }

        onboarding.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapOnboarding(onboarding);
    }

    public async Task<UserOnboardingDto> CompleteOnboardingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        var onboarding = entities.Onboarding;

        onboarding.Status = UserDefaults.OnboardingCompleted;
        onboarding.CurrentStep = Math.Max(onboarding.CurrentStep, 100);
        onboarding.CompletedAt = DateTime.UtcNow;
        onboarding.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);
        return MapOnboarding(onboarding);
    }

    public async Task<UserActivityDto> PingActivityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        var activity = entities.Activity;

        activity.LastActiveAt = DateTime.UtcNow;
        activity.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);
        return MapActivity(activity);
    }

    public async Task<UserActivityDto> UpdateActivityAsync(Guid userId, UpdateActivityRequest request, CancellationToken cancellationToken = default)
    {
        var entities = await EnsureUserDataAsync(userId, suggestedDisplayName: null, cancellationToken);
        var activity = entities.Activity;

        if (request.LastActiveWorkspaceId.HasValue)
        {
            activity.LastActiveWorkspaceId = request.LastActiveWorkspaceId.Value;
        }

        if (request.TaskCompletedCount.HasValue)
        {
            activity.TaskCompletedCount = request.TaskCompletedCount.Value;
        }

        if (request.CommentCount.HasValue)
        {
            activity.CommentCount = request.CommentCount.Value;
        }

        if (request.WorkspaceVisitCount.HasValue)
        {
            activity.WorkspaceVisitCount = request.WorkspaceVisitCount.Value;
        }

        activity.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapActivity(activity);
    }

    public async Task<IReadOnlyList<PublicUserDto>> GetPublicProfilesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = userIds.Distinct().Where(id => id != Guid.Empty).ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var profiles = await _userRepository.GetProfilesByUserIdsAsync(distinctIds, cancellationToken);
        var profileByUserId = profiles.ToDictionary(x => x.UserId, x => x);
        var identityById = await _dbContext.IdentityDirectoryEntries
            .AsNoTracking()
            .Where(entry => entry.IsActive && distinctIds.Contains(entry.UserId))
            .ToDictionaryAsync(entry => entry.UserId, cancellationToken);

        var result = new List<PublicUserDto>(distinctIds.Count);
        foreach (var userId in distinctIds)
        {
            profileByUserId.TryGetValue(userId, out var profile);
            identityById.TryGetValue(userId, out var identity);
            result.Add(MapPublicUser(userId, profile, identity));
        }

        return result;
    }

    public async Task<IReadOnlyList<PublicUserDto>> SearchUsersAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var normalizedLimit = Math.Clamp(limit, 1, 50);
        var loweredQuery = normalized.ToLowerInvariant();
        var identityUsers = await _dbContext.IdentityDirectoryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.IsActive &&
                (entry.Username.ToLower().Contains(loweredQuery) || entry.Email.ToLower().Contains(loweredQuery)))
            .OrderBy(entry => entry.Username)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        if (identityUsers.Count == 0)
        {
            return [];
        }

        var userIds = identityUsers.Select(x => x.UserId).ToList();
        var profiles = await _userRepository.GetProfilesByUserIdsAsync(userIds, cancellationToken);
        var profileByUserId = profiles.ToDictionary(x => x.UserId, x => x);

        return identityUsers
            .Select(identity =>
            {
                profileByUserId.TryGetValue(identity.UserId, out var profile);
                return MapPublicUser(identity.UserId, profile, identity);
            })
            .ToList();
    }

    private async Task<UserDataBundle> EnsureUserDataAsync(
        Guid userId,
        string? suggestedDisplayName,
        CancellationToken cancellationToken)
    {
        var profile = await _userRepository.GetProfileAsync(userId, cancellationToken);
        var preference = await _userRepository.GetPreferenceAsync(userId, cancellationToken);
        var onboarding = await _userRepository.GetOnboardingAsync(userId, cancellationToken);
        var activity = await _userRepository.GetActivityAsync(userId, cancellationToken);

        var updated = false;

        if (profile is null)
        {
            profile = new UserProfile
            {
                UserId = userId,
                DisplayName = NormalizeOptionalText(suggestedDisplayName),
                AvatarUrl = null,
                Bio = null,
                Timezone = UserDefaults.DefaultTimezone,
                Language = NormalizeLanguage(UserDefaults.DefaultLanguage),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _userRepository.AddProfile(profile);
            updated = true;
        }

        if (preference is null)
        {
            preference = new UserPreference
            {
                UserId = userId,
                Theme = NormalizeTheme(UserDefaults.DefaultTheme),
                DefaultBoardBackgroundJson = null,
                EmailNotifications = true,
                PushNotifications = true,
                WeeklyDigest = false,
                UpdatedAt = DateTime.UtcNow
            };
            _userRepository.AddPreference(preference);
            updated = true;
        }

        if (onboarding is null)
        {
            onboarding = new UserOnboarding
            {
                UserId = userId,
                Status = UserDefaults.OnboardingNotStarted,
                CurrentStep = 0,
                CompletedAt = null,
                UpdatedAt = DateTime.UtcNow
            };
            _userRepository.AddOnboarding(onboarding);
            updated = true;
        }

        if (activity is null)
        {
            activity = new UserActivity
            {
                UserId = userId,
                LastActiveAt = DateTime.UtcNow,
                LastActiveWorkspaceId = null,
                TaskCompletedCount = 0,
                CommentCount = 0,
                WorkspaceVisitCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _userRepository.AddActivity(activity);
            updated = true;
        }

        if (updated)
        {
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        var normalized = NormalizeEntities(profile, preference);
        if (normalized)
        {
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return new UserDataBundle(profile, preference, onboarding, activity);
    }

    private static PublicUserDto MapPublicUser(Guid userId, UserProfile? profile, IdentityDirectoryEntry? identity)
    {
        var fallbackName = !string.IsNullOrWhiteSpace(identity?.Username)
            ? identity.Username
            : $"user-{userId.ToString("N")[..8]}";

        return new PublicUserDto
        {
            UserId = userId,
            Username = identity?.Username,
            DisplayName = string.IsNullOrWhiteSpace(profile?.DisplayName) ? fallbackName : profile!.DisplayName!,
            AvatarUrl = profile?.AvatarUrl,
            EmailMasked = MaskEmail(identity?.Email)
        };
    }

    private static UserProfileDto MapProfile(UserProfile profile, string? usernameFallback)
    {
        var displayName = string.IsNullOrWhiteSpace(profile.DisplayName)
            ? (usernameFallback ?? string.Empty)
            : profile.DisplayName;

        return new UserProfileDto
        {
            UserId = profile.UserId,
            DisplayName = displayName,
            AvatarUrl = profile.AvatarUrl,
            Bio = profile.Bio,
            Timezone = profile.Timezone,
            Language = NormalizeLanguage(profile.Language),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }

    private static UserPreferencesDto MapPreferences(UserPreference preference)
    {
        return new UserPreferencesDto
        {
            UserId = preference.UserId,
            Theme = NormalizeTheme(preference.Theme),
            DefaultBoardBackground = MapBoardBackground(preference.DefaultBoardBackgroundJson),
            EmailNotifications = preference.EmailNotifications,
            PushNotifications = preference.PushNotifications,
            WeeklyDigest = preference.WeeklyDigest,
            UpdatedAt = preference.UpdatedAt
        };
    }

    private static UserOnboardingDto MapOnboarding(UserOnboarding onboarding)
    {
        return new UserOnboardingDto
        {
            UserId = onboarding.UserId,
            Status = onboarding.Status,
            CurrentStep = onboarding.CurrentStep,
            CompletedAt = onboarding.CompletedAt,
            UpdatedAt = onboarding.UpdatedAt
        };
    }

    private static UserActivityDto MapActivity(UserActivity activity)
    {
        return new UserActivityDto
        {
            UserId = activity.UserId,
            LastActiveAt = activity.LastActiveAt,
            LastActiveWorkspaceId = activity.LastActiveWorkspaceId,
            TaskCompletedCount = activity.TaskCompletedCount,
            CommentCount = activity.CommentCount,
            WorkspaceVisitCount = activity.WorkspaceVisitCount,
            UpdatedAt = activity.UpdatedAt
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool NormalizeEntities(UserProfile profile, UserPreference preference)
    {
        var changed = false;

        var normalizedLanguage = NormalizeLanguage(profile.Language);
        if (!string.Equals(profile.Language, normalizedLanguage, StringComparison.Ordinal))
        {
            profile.Language = normalizedLanguage;
            profile.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        var normalizedTheme = NormalizeTheme(preference.Theme);
        if (!string.Equals(preference.Theme, normalizedTheme, StringComparison.Ordinal))
        {
            preference.Theme = normalizedTheme;
            preference.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        var normalizedBoardBackground = NormalizeBoardBackground(preference.DefaultBoardBackgroundJson);
        if (!string.Equals(preference.DefaultBoardBackgroundJson, normalizedBoardBackground, StringComparison.Ordinal))
        {
            preference.DefaultBoardBackgroundJson = normalizedBoardBackground;
            preference.UpdatedAt = DateTime.UtcNow;
            changed = true;
        }

        return changed;
    }

    private static string NormalizeLanguage(string? language)
    {
        var normalized = language?.Trim().ToLowerInvariant();
        return normalized is not null && SupportedLanguages.Contains(normalized)
            ? normalized
            : UserDefaults.DefaultLanguage;
    }

    private static string NormalizeTheme(string? theme)
    {
        var normalized = theme?.Trim().ToLowerInvariant();
        return normalized is not null && SupportedThemes.Contains(normalized)
            ? normalized
            : UserDefaults.DefaultTheme;
    }

    private static string? NormalizeBoardBackground(BoardBackgroundPreferenceRequest? background)
    {
        if (background is null)
        {
            return null;
        }

        return NormalizeBoardBackground(new BoardBackgroundPayload
        {
            Type = background.Type,
            Value = background.Value
        });
    }

    private static string? NormalizeBoardBackground(string? serializedBackground)
    {
        if (string.IsNullOrWhiteSpace(serializedBackground))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<BoardBackgroundPayload>(serializedBackground);
            return NormalizeBoardBackground(parsed);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeBoardBackground(BoardBackgroundPayload? background)
    {
        var normalizedType = background?.Type?.Trim().ToLowerInvariant();
        var normalizedValue = background?.Value?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedType) || string.IsNullOrWhiteSpace(normalizedValue))
        {
            return null;
        }

        if (!SupportedBoardBackgroundTypes.Contains(normalizedType))
        {
            return null;
        }

        return JsonSerializer.Serialize(new BoardBackgroundPayload
        {
            Type = normalizedType,
            Value = normalizedValue
        });
    }

    private static BoardBackgroundPreferenceDto? MapBoardBackground(string? serializedBackground)
    {
        var normalized = NormalizeBoardBackground(serializedBackground);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<BoardBackgroundPayload>(normalized);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Type) || string.IsNullOrWhiteSpace(payload.Value))
            {
                return null;
            }

            return new BoardBackgroundPreferenceDto
            {
                Type = payload.Type,
                Value = payload.Value
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var parts = email.Split('@');
        if (parts.Length != 2)
        {
            return null;
        }

        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 2)
        {
            return $"{local[0]}***@{domain}";
        }

        var visiblePrefix = local[..2];
        var visibleSuffix = local.Length > 3 ? local[^1].ToString() : string.Empty;
        return $"{visiblePrefix}***{visibleSuffix}@{domain}";
    }

    private sealed record UserDataBundle(
        UserProfile Profile,
        UserPreference Preferences,
        UserOnboarding Onboarding,
        UserActivity Activity);

    private sealed class BoardBackgroundPayload
    {
        public string? Type { get; set; }
        public string? Value { get; set; }
    }
}
