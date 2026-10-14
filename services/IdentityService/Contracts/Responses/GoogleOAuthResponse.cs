namespace IdentityService.Contracts.Responses;

public sealed class GoogleOAuthResponse
{
    public string Subject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Picture { get; set; } = string.Empty;
    public string? Locale { get; set; }
}
