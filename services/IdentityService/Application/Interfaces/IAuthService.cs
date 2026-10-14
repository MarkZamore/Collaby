using IdentityService.Contracts.Requests;
using IdentityService.Contracts.Responses;

namespace IdentityService.Application.Interfaces;

public interface IAuthService
{
    GoogleAuthConfigResponse GetGoogleAuthConfig();
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<GoogleAuthStartResponse> AuthenticateWithGoogleAsync(GoogleAuthRequest request);
    Task<AuthResponse> CompleteGoogleRegistrationAsync(CompleteGoogleRegistrationRequest request);
    Task<AuthResponse> LoginWithTrustedEmailAsync(string email);
    Task<AuthResponse> RefreshAsync(RefreshRequest request);
    Task<bool> LogoutAsync(string refreshToken);
    Task<UserDto?> GetUserAsync(Guid userId);
    Task<bool> CheckNicknameAvailabilityAsync(string nickname);
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AuthResponse> ValidateResetPasswordTokenAsync(ValidateResetPasswordTokenRequest request);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
}
