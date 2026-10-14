using System.Security.Cryptography;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using IdentityService.Contracts.Requests;
using IdentityService.Contracts.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using IdentityService.Infrastructure.Data;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;

namespace IdentityService.Application.Services;

public class AuthService : IAuthService
{
    private readonly IdentityDbContext _dbContext;
    private readonly MongoDbContext _mongoContext;
    private readonly IJwtService _jwtService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly IGoogleOAuthService _googleOAuthService;
    private readonly IGoogleRegistrationTokenService _googleRegistrationTokenService;
    private readonly IGoogleNicknameGenerator _googleNicknameGenerator;
    private readonly ISystemAdminRoster _systemAdminRoster;
    private readonly IIdentityDirectoryOutboxWriter _identityDirectoryOutboxWriter;
    private readonly GoogleOptions _googleOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IdentityDbContext dbContext,
        MongoDbContext mongoContext,
        IJwtService jwtService,
        IPasswordHasher passwordHasher,
        IEmailVerificationService emailVerificationService,
        IGoogleOAuthService googleOAuthService,
        IGoogleRegistrationTokenService googleRegistrationTokenService,
        IGoogleNicknameGenerator googleNicknameGenerator,
        ISystemAdminRoster systemAdminRoster,
        IIdentityDirectoryOutboxWriter identityDirectoryOutboxWriter,
        IOptions<GoogleOptions> googleOptions,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _mongoContext = mongoContext;
        _jwtService = jwtService;
        _passwordHasher = passwordHasher;
        _emailVerificationService = emailVerificationService;
        _googleOAuthService = googleOAuthService;
        _googleRegistrationTokenService = googleRegistrationTokenService;
        _googleNicknameGenerator = googleNicknameGenerator;
        _systemAdminRoster = systemAdminRoster;
        _identityDirectoryOutboxWriter = identityDirectoryOutboxWriter;
        _googleOptions = googleOptions.Value;
        _logger = logger;
    }

    public GoogleAuthConfigResponse GetGoogleAuthConfig()
    {
        var enabled = !string.IsNullOrWhiteSpace(_googleOptions.ClientId);
        return new GoogleAuthConfigResponse
        {
            Enabled = enabled,
            ClientId = enabled ? _googleOptions.ClientId : null
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.Nickname);

        if (existingUser != null)
        {
            return new AuthResponse
            {
                Success = false,
                Message = existingUser.Email == request.Email ? "Email already registered" : "Nickname already taken"
            };
        }

        var verificationToken = _emailVerificationService.GenerateVerificationToken();
        _logger.LogInformation("Generated verification token for email: {Email}", request.Email);

        var user = new User
        {
            Username = request.Nickname,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsEmailVerified = false,
            Provider = AuthProvider.Local,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        _identityDirectoryOutboxWriter.Enqueue(user, "activated");
        await _dbContext.SaveChangesAsync();

        var emailVerification = new EmailVerification
        {
            UserId = user.Id,
            Token = verificationToken,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.EmailVerifications.Add(emailVerification);
        await _dbContext.SaveChangesAsync();

        await _emailVerificationService.SendVerificationEmailAsync(user.Email, user.Id.ToString(), verificationToken);

        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        await AuditLoginAsync(user.Id, user.Email, "Registration", true, null, null, null);

        return new AuthResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = MapUser(user)
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.EmailOrUsername || u.Username == request.EmailOrUsername);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            await AuditLoginAsync(null, request.EmailOrUsername, "Login", false, "Invalid credentials", null, null);
            return new AuthResponse { Success = false, Code = "invalid_credentials", Message = "Invalid credentials" };
        }

        if (!user.IsActive)
        {
            await AuditLoginAsync(user.Id, user.Email, "Login", false, "User account is inactive", null, null);
            return new AuthResponse { Success = false, Code = "user_inactive", Message = "User account is inactive or blocked" };
        }

        if (!user.IsEmailVerified)
        {
            await AuditLoginAsync(user.Id, user.Email, "Login", false, "Email not verified", null, null);
            return new AuthResponse { Success = false, Code = "email_not_verified", Message = "Email not verified. Please verify your email before logging in" };
        }

        return await IssueAuthenticatedResponseAsync(user, "Login");
    }

    public async Task<GoogleAuthStartResponse> AuthenticateWithGoogleAsync(GoogleAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            return new GoogleAuthStartResponse
            {
                Success = false,
                Code = "google_auth_disabled",
                Message = "Google Sign-In is unavailable"
            };
        }

        GoogleOAuthResponse googleIdentity;
        try
        {
            googleIdentity = await _googleOAuthService.ValidateGoogleTokenAsync(request.IdToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google ID token validation failed");
            return new GoogleAuthStartResponse
            {
                Success = false,
                Code = "invalid_google_token",
                Message = "Google identity could not be verified"
            };
        }

        if (string.IsNullOrWhiteSpace(googleIdentity.Subject) || string.IsNullOrWhiteSpace(googleIdentity.Email))
        {
            return new GoogleAuthStartResponse
            {
                Success = false,
                Code = "invalid_google_token",
                Message = "Google identity could not be verified"
            };
        }

        if (!googleIdentity.EmailVerified)
        {
            return new GoogleAuthStartResponse
            {
                Success = false,
                Code = "google_email_not_verified",
                Message = "Google email is not verified"
            };
        }

        var linkedAccount = await _dbContext.OAuthAccounts
            .Include(account => account.User)
            .FirstOrDefaultAsync(account =>
                account.Provider == AuthProvider.Google &&
                account.ProviderId == googleIdentity.Subject);

        if (linkedAccount != null)
        {
            var linkedResult = await SignInWithGoogleAccountAsync(linkedAccount, googleIdentity, "GoogleLogin");
            return ToGoogleAuthStartResponse(linkedResult);
        }

        var existingUser = await _dbContext.Users
            .Include(user => user.OAuthAccounts)
            .FirstOrDefaultAsync(user => user.Email == googleIdentity.Email);

        if (existingUser != null)
        {
            var linkedResult = await LinkExistingUserAndSignInAsync(existingUser, googleIdentity, "GoogleLogin");
            return ToGoogleAuthStartResponse(linkedResult);
        }

        var registrationToken = _googleRegistrationTokenService.GenerateToken(new GoogleRegistrationTokenPayload
        {
            Subject = googleIdentity.Subject,
            Email = googleIdentity.Email,
            EmailVerified = googleIdentity.EmailVerified,
            Name = googleIdentity.Name,
            Picture = string.IsNullOrWhiteSpace(googleIdentity.Picture) ? null : googleIdentity.Picture,
            Locale = googleIdentity.Locale
        });

        return new GoogleAuthStartResponse
        {
            Success = true,
            RequiresNickname = true,
            RegistrationToken = registrationToken,
            SuggestedNickname = await GenerateAvailableNicknameAsync(googleIdentity),
            Profile = MapGoogleProfile(googleIdentity.Email, googleIdentity.Name, googleIdentity.Picture, googleIdentity.Locale)
        };
    }

    public async Task<AuthResponse> CompleteGoogleRegistrationAsync(CompleteGoogleRegistrationRequest request)
    {
        GoogleRegistrationTokenPayload googleIdentity;
        try
        {
            googleIdentity = _googleRegistrationTokenService.ValidateToken(request.RegistrationToken);
        }
        catch (SecurityTokenExpiredException)
        {
            return new AuthResponse
            {
                Success = false,
                Code = "expired_registration_token",
                Message = "Google registration session expired"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google registration token validation failed");
            return new AuthResponse
            {
                Success = false,
                Code = "invalid_registration_token",
                Message = "Google registration session is invalid"
            };
        }

        if (!googleIdentity.EmailVerified)
        {
            return new AuthResponse
            {
                Success = false,
                Code = "google_email_not_verified",
                Message = "Google email is not verified"
            };
        }

        var linkedAccount = await _dbContext.OAuthAccounts
            .Include(account => account.User)
            .FirstOrDefaultAsync(account =>
                account.Provider == AuthProvider.Google &&
                account.ProviderId == googleIdentity.Subject);

        if (linkedAccount != null)
        {
            return await SignInWithGoogleAccountAsync(
                linkedAccount,
                ToGoogleOAuthResponse(googleIdentity),
                "GoogleRegister");
        }

        var existingUser = await _dbContext.Users
            .Include(user => user.OAuthAccounts)
            .FirstOrDefaultAsync(user => user.Email == googleIdentity.Email);

        if (existingUser != null)
        {
            return await LinkExistingUserAndSignInAsync(
                existingUser,
                ToGoogleOAuthResponse(googleIdentity),
                "GoogleRegister");
        }

        var nickname = request.Nickname.Trim();
        if (!await CheckNicknameAvailabilityAsync(nickname))
        {
            return new AuthResponse
            {
                Success = false,
                Code = "nickname_taken",
                Message = "Nickname already taken"
            };
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Username = nickname,
            Email = googleIdentity.Email,
            PasswordHash = _passwordHasher.HashPassword(GenerateUnusablePassword()),
            AvatarUrl = string.IsNullOrWhiteSpace(googleIdentity.Picture) ? null : googleIdentity.Picture,
            Provider = AuthProvider.Google,
            IsEmailVerified = true,
            EmailVerifiedAt = now,
            CreatedAt = now
        };

        var oauthAccount = new OAuthAccount
        {
            User = user,
            Provider = AuthProvider.Google,
            ProviderId = googleIdentity.Subject,
            LinkedAt = now,
            LastUsedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.OAuthAccounts.Add(oauthAccount);
        _identityDirectoryOutboxWriter.Enqueue(user, "activated");

        return await IssueAuthenticatedResponseAsync(user, "GoogleRegister");
    }

    public async Task<AuthResponse> LoginWithTrustedEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new AuthResponse
            {
                Success = false,
                Code = "missing_email",
                Message = "Trusted email is required"
            };
        }

        var normalizedEmail = email.Trim();
        if (!_systemAdminRoster.IsSystemAdminEmail(normalizedEmail))
        {
            await AuditLoginAsync(null, normalizedEmail, "AccessLogin", false, "Email is not allowlisted for system admin access", null, null);
            return new AuthResponse
            {
                Success = false,
                Code = "forbidden",
                Message = "Email is not allowed to access the admin panel"
            };
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            await AuditLoginAsync(null, normalizedEmail, "AccessLogin", false, "User not found", null, null);
            return new AuthResponse
            {
                Success = false,
                Code = "user_not_found",
                Message = "User account was not found"
            };
        }

        if (!user.IsActive)
        {
            await AuditLoginAsync(user.Id, user.Email, "AccessLogin", false, "User account is inactive", null, null);
            return new AuthResponse
            {
                Success = false,
                Code = "user_inactive",
                Message = "User account is inactive or blocked"
            };
        }

        return await IssueAuthenticatedResponseAsync(user, "AccessLogin");
    }

    public async Task<bool> CheckNicknameAvailabilityAsync(string nickname)
    {
        return !await _dbContext.Users.AnyAsync(u => u.Username == nickname);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (refreshToken == null || refreshToken.IsExpired)
        {
            return new AuthResponse { Success = false, Message = "Invalid or expired refresh token" };
        }

        if (refreshToken.RevokedAt.HasValue)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId}.", refreshToken.UserId);
            await RevokeDescendantRefreshTokensAsync(refreshToken, refreshToken.UserId, "Attempted reuse of revoked ancestor token");
            return new AuthResponse { Success = false, Message = "Token has been revoked" };
        }

        var user = refreshToken.User;
        if (!user.IsActive)
        {
            return new AuthResponse { Success = false, Message = "User is inactive" };
        }

        var accessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByToken = newRefreshToken;

        var newRefreshTokenEntity = new RefreshToken
        {
            Token = newRefreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        return new AuthResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            User = MapUser(user)
        };
    }

    private async Task RevokeDescendantRefreshTokensAsync(RefreshToken refreshToken, Guid userId, string reason)
    {
        if (!string.IsNullOrEmpty(refreshToken.ReplacedByToken))
        {
            var childToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken.ReplacedByToken && rt.UserId == userId);

            if (childToken != null && !childToken.RevokedAt.HasValue)
            {
                _logger.LogWarning("Revoking descendant refresh token for user {UserId}. Reason: {Reason}", userId, reason);
                childToken.RevokedAt = DateTime.UtcNow;
                await RevokeDescendantRefreshTokensAsync(childToken, userId, reason);
            }
        }
    }

    public async Task<bool> LogoutAsync(string refreshToken)
    {
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token == null)
        {
            return false;
        }

        token.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await AuditLoginAsync(token.UserId, null, "Logout", true, null, null, null);

        return true;
    }

    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        return user == null ? null : MapUser(user);
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                _logger.LogInformation("Forgot password request for non-existent email: {Email}", request.Email);
                return new AuthResponse
                {
                    Success = true,
                    Message = "If this email exists in our system, you will receive a password reset link"
                };
            }

            var resetToken = GenerateSecureToken();
            var passwordReset = new PasswordReset
            {
                UserId = user.Id,
                Token = resetToken,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedByIp = null,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.PasswordResets.Add(passwordReset);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Password reset token generated for email: {Email}", request.Email);
            var emailSent = await _emailVerificationService.SendPasswordResetEmailAsync(user.Email, resetToken);

            if (!emailSent)
            {
                _logger.LogWarning("Failed to send password reset email to {Email}", user.Email);
            }

            return new AuthResponse
            {
                Success = true,
                Message = "If this email exists in our system, you will receive a password reset link"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ForgotPasswordAsync");
            return new AuthResponse
            {
                Success = false,
                Message = "An error occurred while processing your request"
            };
        }
    }

    public async Task<AuthResponse> ValidateResetPasswordTokenAsync(ValidateResetPasswordTokenRequest request)
    {
        try
        {
            var passwordReset = await _dbContext.PasswordResets
                .FirstOrDefaultAsync(pr =>
                    pr.Token == request.Token &&
                    pr.ExpiresAt > DateTime.UtcNow &&
                    pr.UsedAt == null);

            if (passwordReset == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Code = "invalid_reset_token",
                    Message = "Invalid or expired reset token"
                };
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
                u.Id == passwordReset.UserId &&
                u.Email == request.Email);

            if (user == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Code = "invalid_reset_token",
                    Message = "Invalid or expired reset token"
                };
            }

            return new AuthResponse
            {
                Success = true,
                Code = "reset_token_valid",
                Message = "Reset token is valid"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ValidateResetPasswordTokenAsync");
            return new AuthResponse
            {
                Success = false,
                Code = "reset_token_validation_failed",
                Message = "An error occurred while validating the reset token"
            };
        }
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var passwordReset = await _dbContext.PasswordResets
                .FirstOrDefaultAsync(pr =>
                    pr.Token == request.Token &&
                    pr.ExpiresAt > DateTime.UtcNow &&
                    pr.UsedAt == null);

            if (passwordReset == null)
            {
                _logger.LogWarning("Invalid or expired password reset token");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid or expired reset token"
                };
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
                u.Id == passwordReset.UserId &&
                u.Email == request.Email);

            if (user == null)
            {
                _logger.LogWarning("User not found for password reset");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid or expired reset token"
                };
            }

            user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
            passwordReset.UsedAt = DateTime.UtcNow;

            _dbContext.Users.Update(user);
            _dbContext.PasswordResets.Update(passwordReset);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Password reset successfully for email: {Email}", request.Email);

            return new AuthResponse
            {
                Success = true,
                Message = "Password has been reset successfully. Please login with your new password"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ResetPasswordAsync");
            return new AuthResponse
            {
                Success = false,
                Message = "An error occurred while resetting your password"
            };
        }
    }

    private async Task<AuthResponse> SignInWithGoogleAccountAsync(
        OAuthAccount oauthAccount,
        GoogleOAuthResponse googleIdentity,
        string auditAction)
    {
        var user = oauthAccount.User;
        if (!user.IsActive)
        {
            await AuditLoginAsync(user.Id, user.Email, auditAction, false, "User account is inactive", null, null);
            return new AuthResponse
            {
                Success = false,
                Code = "user_inactive",
                Message = "User account is inactive or blocked"
            };
        }

        PromoteVerifiedGoogleIdentity(user, googleIdentity.Picture, DateTime.UtcNow);
        oauthAccount.LastUsedAt = DateTime.UtcNow;

        return await IssueAuthenticatedResponseAsync(user, auditAction);
    }

    private async Task<AuthResponse> LinkExistingUserAndSignInAsync(
        User user,
        GoogleOAuthResponse googleIdentity,
        string auditAction)
    {
        if (!user.IsActive)
        {
            await AuditLoginAsync(user.Id, user.Email, auditAction, false, "User account is inactive", null, null);
            return new AuthResponse
            {
                Success = false,
                Code = "user_inactive",
                Message = "User account is inactive or blocked"
            };
        }

        var existingGoogleLink = user.OAuthAccounts
            .FirstOrDefault(account => account.Provider == AuthProvider.Google);

        if (existingGoogleLink == null)
        {
            _dbContext.OAuthAccounts.Add(new OAuthAccount
            {
                UserId = user.Id,
                Provider = AuthProvider.Google,
                ProviderId = googleIdentity.Subject,
                LinkedAt = DateTime.UtcNow,
                LastUsedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingGoogleLink.LastUsedAt = DateTime.UtcNow;
        }

        PromoteVerifiedGoogleIdentity(user, googleIdentity.Picture, DateTime.UtcNow);

        return await IssueAuthenticatedResponseAsync(user, auditAction);
    }

    private async Task<AuthResponse> IssueAuthenticatedResponseAsync(User user, string auditAction)
    {
        var accessToken = _jwtService.GenerateAccessToken(user);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync();

        await AuditLoginAsync(user.Id, user.Email, auditAction, true, null, null, null);

        return new AuthResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = MapUser(user)
        };
    }

    private async Task<string> GenerateAvailableNicknameAsync(GoogleOAuthResponse googleIdentity)
    {
        var baseNickname = _googleNicknameGenerator.GenerateBaseNickname(
            googleIdentity.Name,
            googleIdentity.Email,
            googleIdentity.Subject);

        if (await CheckNicknameAvailabilityAsync(baseNickname))
        {
            return baseNickname;
        }

        var suffix = _googleNicknameGenerator.GenerateSuffix(googleIdentity.Subject, googleIdentity.Email);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var currentSuffix = attempt == 0 ? suffix : $"{suffix[..Math.Min(4, suffix.Length)]}{attempt}";
            var candidateBase = baseNickname.Length > 50 - currentSuffix.Length - 1
                ? baseNickname[..(50 - currentSuffix.Length - 1)].TrimEnd('_')
                : baseNickname;
            var candidate = $"{candidateBase}_{currentSuffix}";

            if (await CheckNicknameAvailabilityAsync(candidate))
            {
                return candidate;
            }
        }

        return $"collaby_{_googleNicknameGenerator.GenerateSuffix(googleIdentity.Subject, googleIdentity.Email, 8)}";
    }

    private static void PromoteVerifiedGoogleIdentity(User user, string? picture, DateTime now)
    {
        user.IsEmailVerified = true;
        user.EmailVerifiedAt ??= now;

        if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(picture))
        {
            user.AvatarUrl = picture;
        }
    }

    private static GoogleAuthProfileResponse MapGoogleProfile(string email, string? name, string? picture, string? locale)
    {
        return new GoogleAuthProfileResponse
        {
            Email = email,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            Picture = string.IsNullOrWhiteSpace(picture) ? null : picture,
            Locale = locale
        };
    }

    private static GoogleOAuthResponse ToGoogleOAuthResponse(GoogleRegistrationTokenPayload payload)
    {
        return new GoogleOAuthResponse
        {
            Subject = payload.Subject,
            Email = payload.Email,
            EmailVerified = payload.EmailVerified,
            Name = payload.Name ?? string.Empty,
            Picture = payload.Picture ?? string.Empty,
            Locale = payload.Locale
        };
    }

    private static GoogleAuthStartResponse ToGoogleAuthStartResponse(AuthResponse response)
    {
        return new GoogleAuthStartResponse
        {
            Success = response.Success,
            Code = response.Code,
            Message = response.Message,
            AccessToken = response.AccessToken,
            RefreshToken = response.RefreshToken,
            ExpiresAt = response.ExpiresAt,
            User = response.User
        };
    }

    private async Task AuditLoginAsync(Guid? userId, string? email, string action, bool success, string? errorMessage, string? ipAddress, string? userAgent)
    {
        try
        {
            _logger.LogInformation("Auditing login: {Action} for {Email}, success: {Success}", action, email, success);

            var audit = new LoginAudit
            {
                Id = ObjectId.GenerateNewId().ToString(),
                UserId = userId,
                Email = email ?? string.Empty,
                Action = action,
                Success = success,
                ErrorMessage = errorMessage,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            };

            await _mongoContext.LoginAudits.InsertOneAsync(audit);
            _logger.LogInformation("Audit log inserted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert audit log");
        }
    }

    private UserDto MapUser(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            EmailVerified = user.IsEmailVerified,
            TwoFactorEnabled = user.TwoFactorSetting?.Enabled ?? false,
            AvatarUrl = user.AvatarUrl,
            IsSystemAdmin = _systemAdminRoster.IsSystemAdminEmail(user.Email)
        };
    }

    private static string GenerateSecureToken(int byteLength = 32)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));
    }

    private static string GenerateUnusablePassword()
    {
        return $"google-only::{GenerateSecureToken(32)}";
    }
}
