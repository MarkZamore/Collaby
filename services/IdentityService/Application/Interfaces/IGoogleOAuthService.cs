using IdentityService.Contracts.Responses;

namespace IdentityService.Application.Interfaces;

public interface IGoogleOAuthService
{
    Task<GoogleOAuthResponse> ValidateGoogleTokenAsync(string idToken);
}
