namespace UserService.Contracts.Requests;

public class UpdatePreferencesRequest
{
    public string? Theme { get; set; }
    public BoardBackgroundPreferenceRequest? DefaultBoardBackground { get; set; }
    public bool ResetDefaultBoardBackground { get; set; }
    public bool? EmailNotifications { get; set; }
    public bool? PushNotifications { get; set; }
    public bool? WeeklyDigest { get; set; }
}

public class BoardBackgroundPreferenceRequest
{
    public string? Type { get; set; }
    public string? Value { get; set; }
}

