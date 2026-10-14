namespace UserService.Domain.Entities;

public class UserProfile
{
    public Guid UserId { get; set; }

    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string Timezone { get; set; } = UserDefaults.DefaultTimezone;

    public string Language { get; set; } = UserDefaults.DefaultLanguage;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

