namespace IdentityService.Application.Services;

public sealed class GoogleRegistrationTokenPayload
{
    public string Subject { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public string? Name { get; init; }
    public string? Picture { get; init; }
    public string? Locale { get; init; }
}
