namespace UserService.Contracts.Requests;

public class UpdateProfileRequest
{
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Timezone { get; set; }
    public string? Language { get; set; }
}

