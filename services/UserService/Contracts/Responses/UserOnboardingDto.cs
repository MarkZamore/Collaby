namespace UserService.Contracts.Responses;

public class UserOnboardingDto
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

