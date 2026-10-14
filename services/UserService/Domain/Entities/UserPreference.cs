namespace UserService.Domain.Entities;

public class UserPreference
{
    public Guid UserId { get; set; }

    public string Theme { get; set; } = UserDefaults.DefaultTheme;

    public string? DefaultBoardBackgroundJson { get; set; }

    public bool EmailNotifications { get; set; } = true;

    public bool PushNotifications { get; set; } = true;

    public bool WeeklyDigest { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

