using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Settings;
using IdentityService.Contracts.Requests;
using IdentityService.Contracts.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class AuthServiceGoogleAuthTests
{
    [Fact]
    public async Task AuthenticateWithGoogleAsync_ShouldSignInLinkedGoogleUser()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Email = "linked@collaby.test",
            Username = "linked_user",
            PasswordHash = "hash",
            IsActive = true,
            IsEmailVerified = false
        };
        var oauthAccount = new OAuthAccount
        {
            User = user,
            Provider = AuthProvider.Google,
            ProviderId = "google-subject",
            LinkedAt = DateTime.UtcNow.AddDays(-2)
        };
        dbContext.Users.Add(user);
        dbContext.OAuthAccounts.Add(oauthAccount);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthService(
            dbContext,
            new FakeGoogleOAuthService(new GoogleOAuthResponse
            {
                Subject = "google-subject",
                Email = "linked@collaby.test",
                EmailVerified = true,
                Name = "Linked User",
                Picture = "https://example.test/google-avatar.png"
            }));

        var result = await service.AuthenticateWithGoogleAsync(new GoogleAuthRequest { IdToken = "google-id-token" });

        Assert.True(result.Success);
        Assert.False(result.RequiresNickname);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.User);

        var savedUser = await dbContext.Users.SingleAsync();
        var savedAccount = await dbContext.OAuthAccounts.SingleAsync();
        Assert.True(savedUser.IsEmailVerified);
        Assert.NotNull(savedUser.EmailVerifiedAt);
        Assert.Equal("https://example.test/google-avatar.png", savedUser.AvatarUrl);
        Assert.NotNull(savedAccount.LastUsedAt);
        Assert.Single(dbContext.RefreshTokens);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_ShouldAutoLinkExistingLocalUserByEmail()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Email = "local@collaby.test",
            Username = "local_user",
            PasswordHash = "hash",
            AvatarUrl = "https://example.test/local-avatar.png",
            Provider = AuthProvider.Local,
            IsActive = true,
            IsEmailVerified = false
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = CreateAuthService(
            dbContext,
            new FakeGoogleOAuthService(new GoogleOAuthResponse
            {
                Subject = "google-subject",
                Email = "local@collaby.test",
                EmailVerified = true,
                Name = "Local User",
                Picture = "https://example.test/google-avatar.png"
            }));

        var result = await service.AuthenticateWithGoogleAsync(new GoogleAuthRequest { IdToken = "google-id-token" });

        Assert.True(result.Success);

        var savedUser = await dbContext.Users.Include(item => item.OAuthAccounts).SingleAsync();
        Assert.True(savedUser.IsEmailVerified);
        Assert.Equal("local_user", savedUser.Username);
        Assert.Equal("https://example.test/local-avatar.png", savedUser.AvatarUrl);
        Assert.Equal(AuthProvider.Local, savedUser.Provider);
        Assert.Single(savedUser.OAuthAccounts);
        Assert.Equal("google-subject", savedUser.OAuthAccounts.Single().ProviderId);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_ShouldRejectInactiveExistingUser()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            Email = "inactive@collaby.test",
            Username = "inactive_user",
            PasswordHash = "hash",
            IsActive = false
        });
        await dbContext.SaveChangesAsync();

        var service = CreateAuthService(
            dbContext,
            new FakeGoogleOAuthService(new GoogleOAuthResponse
            {
                Subject = "google-subject",
                Email = "inactive@collaby.test",
                EmailVerified = true
            }));

        var result = await service.AuthenticateWithGoogleAsync(new GoogleAuthRequest { IdToken = "google-id-token" });

        Assert.False(result.Success);
        Assert.Equal("user_inactive", result.Code);
        Assert.Empty(dbContext.OAuthAccounts);
    }

    [Fact]
    public async Task AuthenticateWithGoogleAsync_ShouldReturnNicknameChallenge_ForNewEmail()
    {
        await using var dbContext = CreateDbContext();
        var tokenService = new GoogleRegistrationTokenService(Options.Create(CreateJwtSettings()));
        var service = CreateAuthService(
            dbContext,
            new FakeGoogleOAuthService(new GoogleOAuthResponse
            {
                Subject = "google-subject",
                Email = "new-user@collaby.test",
                EmailVerified = true,
                Name = "José García",
                Picture = "https://example.test/google-avatar.png"
            }),
            tokenService: tokenService);

        var result = await service.AuthenticateWithGoogleAsync(new GoogleAuthRequest { IdToken = "google-id-token" });

        Assert.True(result.Success);
        Assert.True(result.RequiresNickname);
        Assert.Equal("jose_garcia", result.SuggestedNickname);
        Assert.NotNull(result.RegistrationToken);
        Assert.Equal("new-user@collaby.test", result.Profile?.Email);

        var payload = tokenService.ValidateToken(result.RegistrationToken!);
        Assert.Equal("google-subject", payload.Subject);
        Assert.Equal("new-user@collaby.test", payload.Email);
    }

    [Fact]
    public async Task CompleteGoogleRegistrationAsync_ShouldCreateGoogleUser()
    {
        await using var dbContext = CreateDbContext();
        var outboxWriter = new RecordingOutboxWriter();
        var tokenService = new GoogleRegistrationTokenService(Options.Create(CreateJwtSettings()));
        var service = CreateAuthService(
            dbContext,
            new FakeGoogleOAuthService(),
            outboxWriter,
            tokenService);
        var token = tokenService.GenerateToken(new GoogleRegistrationTokenPayload
        {
            Subject = "google-subject",
            Email = "new-google@collaby.test",
            EmailVerified = true,
            Name = "New Google User",
            Picture = "https://example.test/google-avatar.png"
        });

        var result = await service.CompleteGoogleRegistrationAsync(new CompleteGoogleRegistrationRequest
        {
            RegistrationToken = token,
            Nickname = "new_google_user"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.User);

        var savedUser = await dbContext.Users.Include(item => item.OAuthAccounts).SingleAsync();
        Assert.Equal(AuthProvider.Google, savedUser.Provider);
        Assert.True(savedUser.IsEmailVerified);
        Assert.NotNull(savedUser.EmailVerifiedAt);
        Assert.Equal("https://example.test/google-avatar.png", savedUser.AvatarUrl);
        Assert.Single(savedUser.OAuthAccounts);
        Assert.Single(dbContext.RefreshTokens);
        Assert.Single(outboxWriter.EnqueuedUsers);
        Assert.False(new PasswordHasher().VerifyPassword("Secret123!", savedUser.PasswordHash));
    }

    [Fact]
    public async Task CompleteGoogleRegistrationAsync_ShouldRejectTakenNickname()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(new User
        {
            Email = "existing@collaby.test",
            Username = "taken_name",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var tokenService = new GoogleRegistrationTokenService(Options.Create(CreateJwtSettings()));
        var service = CreateAuthService(dbContext, new FakeGoogleOAuthService(), tokenService: tokenService);
        var token = tokenService.GenerateToken(new GoogleRegistrationTokenPayload
        {
            Subject = "google-subject",
            Email = "another@collaby.test",
            EmailVerified = true
        });

        var result = await service.CompleteGoogleRegistrationAsync(new CompleteGoogleRegistrationRequest
        {
            RegistrationToken = token,
            Nickname = "taken_name"
        });

        Assert.False(result.Success);
        Assert.Equal("nickname_taken", result.Code);
    }

    [Fact]
    public async Task CompleteGoogleRegistrationAsync_ShouldRejectExpiredOrInvalidRegistrationToken()
    {
        await using var dbContext = CreateDbContext();
        var tokenService = new GoogleRegistrationTokenService(Options.Create(CreateJwtSettings()));
        var service = CreateAuthService(dbContext, new FakeGoogleOAuthService(), tokenService: tokenService);

        var invalidResult = await service.CompleteGoogleRegistrationAsync(new CompleteGoogleRegistrationRequest
        {
            RegistrationToken = "not-a-valid-token",
            Nickname = "google_user"
        });

        Assert.False(invalidResult.Success);
        Assert.Equal("invalid_registration_token", invalidResult.Code);

        var expiredToken = CreateExpiredRegistrationToken();
        var expiredResult = await service.CompleteGoogleRegistrationAsync(new CompleteGoogleRegistrationRequest
        {
            RegistrationToken = expiredToken,
            Nickname = "google_user"
        });

        Assert.False(expiredResult.Success);
        Assert.Equal("expired_registration_token", expiredResult.Code);
    }

    private static AuthService CreateAuthService(
        IdentityDbContext dbContext,
        FakeGoogleOAuthService googleOAuthService,
        RecordingOutboxWriter? outboxWriter = null,
        GoogleRegistrationTokenService? tokenService = null)
    {
        var jwtSettings = CreateJwtSettings();
        var roster = new TestSystemAdminRoster();
        var jwtOptions = Options.Create(jwtSettings);
        var mongoContext = CreateMongoContext();

        return new AuthService(
            dbContext,
            mongoContext,
            new JwtService(jwtOptions, roster),
            new PasswordHasher(),
            new StubEmailVerificationService(),
            googleOAuthService,
            tokenService ?? new GoogleRegistrationTokenService(jwtOptions),
            new GoogleNicknameGenerator(),
            roster,
            outboxWriter ?? new RecordingOutboxWriter(),
            Options.Create(new GoogleOptions { ClientId = "test-google-client-id" }),
            NullLogger<AuthService>.Instance);
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"identity-google-tests-{Guid.NewGuid():N}")
            .Options;
        return new IdentityDbContext(options);
    }

    private static MongoDbContext CreateMongoContext()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mongo"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=10&connectTimeoutMS=10&socketTimeoutMS=10",
                ["Mongo:Database"] = $"identity-google-tests-{Guid.NewGuid():N}"
            })
            .Build();

        return new MongoDbContext(configuration);
    }

    private static JwtSettings CreateJwtSettings()
    {
        return new JwtSettings
        {
            Issuer = "identity-tests",
            Audience = "identity-tests",
            SigningKey = "01234567890123456789012345678901",
            AccessTokenLifetimeMinutes = 60
        };
    }

    private static string CreateExpiredRegistrationToken()
    {
        var settings = CreateJwtSettings();
        var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims:
            [
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, "expired-subject"),
                new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, "expired@collaby.test"),
                new System.Security.Claims.Claim("purpose", "google-registration"),
                new System.Security.Claims.Claim("email_verified", bool.TrueString)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeGoogleOAuthService : IGoogleOAuthService
    {
        private readonly GoogleOAuthResponse? _response;

        public FakeGoogleOAuthService()
        {
        }

        public FakeGoogleOAuthService(GoogleOAuthResponse response)
        {
            _response = response;
        }

        public Task<GoogleOAuthResponse> ValidateGoogleTokenAsync(string idToken)
        {
            if (_response == null)
            {
                throw new InvalidOperationException("No Google response configured");
            }

            return Task.FromResult(_response);
        }
    }

    private sealed class StubEmailVerificationService : IEmailVerificationService
    {
        public string GenerateVerificationToken() => "token";

        public Task<(bool Success, string Message, string? Code)> ResendVerificationEmailAsync(string email)
            => Task.FromResult<(bool Success, string Message, string? Code)>((true, "resent", null));

        public Task<bool> SendPasswordResetEmailAsync(string email, string resetToken)
            => Task.FromResult(true);

        public Task<bool> SendVerificationEmailAsync(string email, string userId, string verificationToken)
            => Task.FromResult(true);

        public Task<(bool Success, string Message, string? Code)> VerifyEmailAsync(string token)
            => Task.FromResult<(bool Success, string Message, string? Code)>((true, "verified", null));
    }

    private sealed class RecordingOutboxWriter : IIdentityDirectoryOutboxWriter
    {
        public List<Guid> EnqueuedUsers { get; } = [];

        public void Enqueue(User user, string lifecycleState)
        {
            EnqueuedUsers.Add(user.Id);
        }
    }

    private sealed class TestSystemAdminRoster : Collaby.ServiceDefaults.SystemAdministration.ISystemAdminRoster
    {
        public IReadOnlyCollection<string> GetEmails() => [];

        public bool IsSystemAdminEmail(string? email) => false;

        public bool IsSystemAdmin(System.Security.Claims.ClaimsPrincipal principal) => false;
    }
}
