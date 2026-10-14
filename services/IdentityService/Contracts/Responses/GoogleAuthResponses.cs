namespace IdentityService.Contracts.Responses;

public sealed class GoogleAuthConfigResponse
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
}

public sealed class GoogleAuthProfileResponse
{
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Picture { get; set; }
    public string? Locale { get; set; }
}

public sealed class GoogleAuthStartResponse : AuthResponse
{
    public bool RequiresNickname { get; set; }
    public string? RegistrationToken { get; set; }
    public string? SuggestedNickname { get; set; }
    public GoogleAuthProfileResponse? Profile { get; set; }
}
