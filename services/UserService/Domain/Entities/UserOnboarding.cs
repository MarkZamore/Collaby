namespace UserService.Domain.Entities;

public class UserOnboarding
{
    public Guid UserId { get; set; }

    public string Status { get; set; } = UserDefaults.OnboardingNotStarted;

    public int CurrentStep { get; set; } = 0;

    public DateTime? CompletedAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

