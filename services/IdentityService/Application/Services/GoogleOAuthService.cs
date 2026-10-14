using Google.Apis.Auth;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using IdentityService.Contracts.Responses;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class GoogleOAuthService : IGoogleOAuthService
{
    private readonly GoogleOptions _googleOptions;

    public GoogleOAuthService(IOptions<GoogleOptions> googleOptions)
    {
        _googleOptions = googleOptions.Value;
    }

    public async Task<GoogleOAuthResponse> ValidateGoogleTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            throw new InvalidOperationException("Google Sign-In is not configured");
        }

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_googleOptions.ClientId]
            });

        return new GoogleOAuthResponse
        {
            Subject = payload.Subject,
            Email = payload.Email ?? string.Empty,
            EmailVerified = payload.EmailVerified,
            Name = payload.Name ?? string.Empty,
            Picture = payload.Picture ?? string.Empty,
            Locale = payload.Locale
        };
    }
}
