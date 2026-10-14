namespace UserService.Contracts.Responses;

public class PublicUserDto
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? EmailMasked { get; set; }
}

