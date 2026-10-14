using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using IdentityService.Contracts.Requests;
using IdentityService.Contracts.Responses;
using IdentityService.Controllers;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Xunit;

namespace IdentityService.Tests.Integration;

public sealed class IdentityServiceTestServer : IAsyncLifetime
{
    private WebApplication? _app;
    private StubAuthService? _authService;

    public HttpClient Client { get; private set; } = null!;
    public StubAuthService AuthService => _authService ?? throw new InvalidOperationException("Test server is not initialized");

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.Configure<JwtSettings>(options =>
        {
            options.Issuer = "tests";
            options.Audience = "tests";
            options.SigningKey = "tests-signing-key";
            options.AccessTokenLifetimeMinutes = 60;
        });
        var databaseName = $"identity-tests-{Guid.NewGuid():N}";
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton<ISystemAdminRoster, StubSystemAdminRoster>();
        builder.Services.AddOptions<InternalServiceAuthOptions>()
            .Configure(options =>
            {
                options.AllowedCallers["user-service"] = "test-user-key";
            });
        builder.Services.AddSingleton<InternalServiceRequestAuthenticator>();
        builder.Services.AddSingleton<StubAuthService>();
        builder.Services.AddSingleton<IAuthService>(serviceProvider => serviceProvider.GetRequiredService<StubAuthService>());
        builder.Services.AddSingleton<IEmailVerificationService, StubEmailVerificationService>();

        _app = builder.Build();
        _authService = _app.Services.GetRequiredService<StubAuthService>();
        await SeedIdentityDirectoryAsync(_app.Services);
        _app.UsePathBase("/api/auth");
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();

        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static async Task SeedIdentityDirectoryAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        dbContext.Users.AddRange(
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "founder@collaby.test",
                Username = "allowlisted-admin",
                PasswordHash = "hash",
                IsActive = true
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "member@collaby.test",
                Username = "member",
                PasswordHash = "hash",
                IsActive = true
            });

        await dbContext.SaveChangesAsync();
    }

    public sealed class StubAuthService : IAuthService
    {
        private static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public GoogleAuthConfigResponse GoogleConfig { get; private set; } = new()
        {
            Enabled = true,
            ClientId = "test-google-client-id"
        };

        public Func<GoogleAuthRequest, Task<GoogleAuthStartResponse>> GoogleAuthHandler { get; private set; } =
            _ => Task.FromResult(new GoogleAuthStartResponse
            {
                Success = true,
                AccessToken = "test-access-token",
                RefreshToken = "test-refresh-token",
                User = CreateUser(DefaultUserId)
            });

        public Func<CompleteGoogleRegistrationRequest, Task<AuthResponse>> CompleteGoogleRegistrationHandler { get; private set; } =
            _ => Task.FromResult(CreateAuthResponse(DefaultUserId));
        public Func<ValidateResetPasswordTokenRequest, Task<AuthResponse>> ValidateResetPasswordTokenHandler { get; private set; } =
            _ => Task.FromResult(new AuthResponse
            {
                Success = true,
                Code = "reset_token_valid",
                Message = "Reset token is valid"
            });

        public void Reset()
        {
            GoogleConfig = new GoogleAuthConfigResponse
            {
                Enabled = true,
                ClientId = "test-google-client-id"
            };
            GoogleAuthHandler = _ => Task.FromResult(new GoogleAuthStartResponse
            {
                Success = true,
                AccessToken = "test-access-token",
                RefreshToken = "test-refresh-token",
                User = CreateUser(DefaultUserId)
            });
            CompleteGoogleRegistrationHandler = _ => Task.FromResult(CreateAuthResponse(DefaultUserId));
            ValidateResetPasswordTokenHandler = _ => Task.FromResult(new AuthResponse
            {
                Success = true,
                Code = "reset_token_valid",
                Message = "Reset token is valid"
            });
        }

        public void SetGoogleConfig(bool enabled, string? clientId)
        {
            GoogleConfig = new GoogleAuthConfigResponse
            {
                Enabled = enabled,
                ClientId = clientId
            };
        }

        public void SetGoogleAuthHandler(Func<GoogleAuthRequest, Task<GoogleAuthStartResponse>> handler)
        {
            GoogleAuthHandler = handler;
        }

        public void SetCompleteGoogleRegistrationHandler(Func<CompleteGoogleRegistrationRequest, Task<AuthResponse>> handler)
        {
            CompleteGoogleRegistrationHandler = handler;
        }

        public void SetValidateResetPasswordTokenHandler(Func<ValidateResetPasswordTokenRequest, Task<AuthResponse>> handler)
        {
            ValidateResetPasswordTokenHandler = handler;
        }

        public GoogleAuthConfigResponse GetGoogleAuthConfig() => GoogleConfig;

        public Task<bool> CheckNicknameAvailabilityAsync(string nickname) => Task.FromResult(true);

        public Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
            => Task.FromResult(new AuthResponse { Success = true });

        public Task<AuthResponse> ValidateResetPasswordTokenAsync(ValidateResetPasswordTokenRequest request)
            => ValidateResetPasswordTokenHandler(request);

        public Task<GoogleAuthStartResponse> AuthenticateWithGoogleAsync(GoogleAuthRequest request)
            => GoogleAuthHandler(request);

        public Task<AuthResponse> CompleteGoogleRegistrationAsync(CompleteGoogleRegistrationRequest request)
            => CompleteGoogleRegistrationHandler(request);

        public Task<UserDto?> GetUserAsync(Guid userId) => Task.FromResult<UserDto?>(CreateUser(userId));

        public Task<AuthResponse> LoginAsync(LoginRequest request)
            => Task.FromResult(CreateAuthResponse(DefaultUserId));

        public Task<AuthResponse> LoginWithTrustedEmailAsync(string email)
            => Task.FromResult(CreateAuthResponse(DefaultUserId));

        public Task<bool> LogoutAsync(string refreshToken) => Task.FromResult(true);

        public Task<AuthResponse> RefreshAsync(RefreshRequest request)
            => Task.FromResult(CreateAuthResponse(DefaultUserId));

        public Task<AuthResponse> RegisterAsync(RegisterRequest request)
            => Task.FromResult(new AuthResponse { Success = true });

        public Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
            => Task.FromResult(new AuthResponse { Success = true });

        private static AuthResponse CreateAuthResponse(Guid userId)
        {
            return new AuthResponse
            {
                Success = true,
                AccessToken = "test-access-token",
                RefreshToken = "test-refresh-token",
                User = CreateUser(userId)
            };
        }

        private static UserDto CreateUser(Guid userId)
        {
            return new UserDto
            {
                Id = userId,
                Username = "allowlisted-admin",
                Email = "founder@collaby.test",
                IsSystemAdmin = true
            };
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

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = authorization["Bearer ".Length..].Trim();
            if (!Guid.TryParse(token, out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.Email, "founder@collaby.test"),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class StubSystemAdminRoster : ISystemAdminRoster
    {
        public IReadOnlyCollection<string> GetEmails() => ["founder@collaby.test"];

        public bool IsSystemAdminEmail(string? email) =>
            string.Equals(email, "founder@collaby.test", StringComparison.OrdinalIgnoreCase);

        public bool IsSystemAdmin(ClaimsPrincipal principal) =>
            IsSystemAdminEmail(principal.FindFirst(ClaimTypes.Email)?.Value);
    }
}
