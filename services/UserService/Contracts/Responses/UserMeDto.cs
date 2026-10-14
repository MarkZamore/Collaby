namespace UserService.Contracts.Responses;

public class UserMeDto
{
    public Guid UserId { get; set; }
    public UserProfileDto Profile { get; set; } = new();
    public UserPreferencesDto Preferences { get; set; } = new();
    public UserOnboardingDto Onboarding { get; set; } = new();
    public UserActivityDto Activity { get; set; } = new();
}

