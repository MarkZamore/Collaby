namespace UserService.Contracts.Responses;

public class UserPreferencesDto
{
    public Guid UserId { get; set; }
    public string Theme { get; set; } = string.Empty;
    public BoardBackgroundPreferenceDto? DefaultBoardBackground { get; set; }
    public bool EmailNotifications { get; set; }
    public bool PushNotifications { get; set; }
    public bool WeeklyDigest { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BoardBackgroundPreferenceDto
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

