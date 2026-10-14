using IdentityService.Application.Services;

namespace IdentityService.Application.Interfaces;

public interface IGoogleRegistrationTokenService
{
    string GenerateToken(GoogleRegistrationTokenPayload payload);
    GoogleRegistrationTokenPayload ValidateToken(string token);
}
