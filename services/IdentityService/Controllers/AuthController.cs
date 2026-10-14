using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using IdentityService.Contracts.Requests;
using IdentityService.Contracts.Responses;
using IdentityService.Infrastructure.Data;
using IdentityService.Infrastructure.Health;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IdentityService.Controllers;

[ApiController]
[Route("")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly JwtSettings _jwtSettings;

    public AuthController(
        IAuthService authService,
        IEmailVerificationService emailVerificationService,
        IOptions<JwtSettings> jwtSettingsOptions)
    {
        _authService = authService;
        _emailVerificationService = emailVerificationService;
        _jwtSettings = jwtSettingsOptions.Value;
    }

    [HttpGet("google/config")]
    public IActionResult GetGoogleConfig()
    {
        return Ok(_authService.GetGoogleAuthConfig());
    }

    [HttpPost("google")]
    public async Task<IActionResult> Google([FromBody] GoogleAuthRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new GoogleAuthStartResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Invalid request data"
            });
        }

        var result = await _authService.AuthenticateWithGoogleAsync(request);

        if (!result.Success)
        {
            return result.Code == "google_auth_disabled"
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, result)
                : Unauthorized(result);
        }

        if (result.RequiresNickname)
        {
            return Ok(result);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        result.ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes);
        result.Message = "Google login successful";
        result.RefreshToken = null;
        return Ok(result);
    }

    [HttpPost("google/register")]
    public async Task<IActionResult> CompleteGoogleRegistration([FromBody] CompleteGoogleRegistrationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Invalid request data"
            });
        }

        var result = await _authService.CompleteGoogleRegistrationAsync(request);

        if (!result.Success)
        {
            return result.Code == "user_inactive"
                ? Unauthorized(result)
                : BadRequest(result);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes),
            User = result.User,
            Code = result.Code,
            Message = "Google registration successful"
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Invalid request data"
            });
        }

        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Message = "Invalid request data"
            });
        }

        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes),
            User = result.User,
            Code = result.Code,
            Message = "Login successful"
        });
    }

    [HttpPost("access-login")]
    public async Task<IActionResult> AccessLogin()
    {
        if (!IsDedicatedAdminHost())
        {
            return NotFound();
        }

        var trustedEmail = Request.Headers["Cf-Access-Authenticated-User-Email"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(trustedEmail))
        {
            return Unauthorized(new AuthResponse
            {
                Success = false,
                Code = "missing_trusted_email",
                Message = "Trusted admin identity is required"
            });
        }

        var result = await _authService.LoginWithTrustedEmailAsync(trustedEmail);

        if (!result.Success)
        {
            return result.Code == "forbidden"
                ? StatusCode(StatusCodes.Status403Forbidden, result)
                : Unauthorized(result);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenLifetimeMinutes),
            User = result.User,
            Code = result.Code,
            Message = "Trusted access login successful"
        });
    }

    [HttpGet("check-nickname")]
    public async Task<IActionResult> CheckNickname([FromQuery] string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            return BadRequest(new CheckNicknameResponse
            {
                Available = false,
                Message = "Nickname is required"
            });
        }

        var available = await _authService.CheckNicknameAvailabilityAsync(nickname.Trim());

        return Ok(new CheckNicknameResponse
        {
            Available = available,
            Message = available ? "Nickname is available" : "Nickname is already taken"
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest? request)
    {
        var refreshToken = !string.IsNullOrEmpty(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Refresh token is required"
            });
        }

        var result = await _authService.RefreshAsync(new RefreshRequest { RefreshToken = refreshToken });

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(new AuthResponse
        {
            Success = true,
            AccessToken = result.AccessToken,
            User = result.User,
            Code = result.Code,
            Message = "Token refreshed successfully"
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request)
    {
        var refreshToken = !string.IsNullOrEmpty(request?.RefreshToken)
            ? request.RefreshToken
            : Request.Cookies["refreshToken"];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _authService.LogoutAsync(refreshToken);
        }

        RemoveRefreshTokenCookie();

        return Ok(new AuthResponse
        {
            Success = true,
            Message = "Logged out successfully"
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var subClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subClaim, out var userId))
        {
            var nameIdentifierClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(nameIdentifierClaim, out userId))
            {
                return Unauthorized(new AuthResponse
                {
                    Success = false,
                    Code = "unauthorized",
                    Message = "Unauthorized"
                });
            }
        }

        var user = await _authService.GetUserAsync(userId);

        if (user == null)
        {
            return NotFound(new AuthResponse
            {
                Success = false,
                Code = "user_not_found",
                Message = "User not found"
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            User = user
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            Secure = HttpContext.RequestServices.GetService<IConfiguration>()?.GetValue<bool?>("Cookies:Secure") ?? Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(14)
        };

        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private void RemoveRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Path = "/",
            Secure = HttpContext.RequestServices.GetService<IConfiguration>()?.GetValue<bool?>("Cookies:Secure") ?? Request.IsHttps,
            SameSite = SameSiteMode.Strict
        });
    }

    private bool IsDedicatedAdminHost()
    {
        foreach (var host in GetRequestHostCandidates())
        {
            if (host.Equals("admin.localhost", StringComparison.OrdinalIgnoreCase)
                || host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerable<string> GetRequestHostCandidates()
    {
        static string NormalizeHost(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var candidate = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            return candidate.Split(':', 2)[0].Trim();
        }

        var directHost = NormalizeHost(Request.Host.Value);
        if (!string.IsNullOrWhiteSpace(directHost))
        {
            yield return directHost;
        }

        var forwardedHost = NormalizeHost(Request.Headers["X-Forwarded-Host"].ToString());
        if (!string.IsNullOrWhiteSpace(forwardedHost))
        {
            yield return forwardedHost;
        }

        var originalHost = NormalizeHost(Request.Headers["X-Original-Host"].ToString());
        if (!string.IsNullOrWhiteSpace(originalHost))
        {
            yield return originalHost;
        }
    }

    [HttpGet("health/live")]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "alive",
            service = "identity"
        });
    }

    [HttpGet("health/ready")]
    public async Task<IActionResult> Ready(
        [FromServices] IdentityServiceReadinessState readinessState,
        [FromServices] IdentityDbContext db,
        [FromServices] MongoDbContext mongo)
    {
        if (!readinessState.IsReady)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                service = "identity",
                reason = readinessState.FailureReason ?? "Startup checks have not completed yet"
            });
        }

        try
        {
            var postgresReady = await db.Database.CanConnectAsync();
            if (!postgresReady)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "unhealthy",
                    service = "identity",
                    reason = "PostgreSQL is unavailable"
                });
            }

            await mongo.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));

            return Ok(new
            {
                status = "ready",
                service = "identity"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unhealthy",
                service = "identity",
                reason = ex.Message
            });
        }
    }

    [HttpGet("health")]
    public Task<IActionResult> Health(
        [FromServices] IdentityServiceReadinessState readinessState,
        [FromServices] IdentityDbContext db,
        [FromServices] MongoDbContext mongo) => Ready(readinessState, db, mongo);

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(request.Token))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "verification_token_required",
                Message = "Verification token is required"
            });
        }

        var (success, message, code) = await _emailVerificationService.VerifyEmailAsync(request.Token);

        if (!success)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = code,
                Message = message
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            Code = code,
            Message = message
        });
    }

    [HttpPost("resend-verification-email")]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(request.Email))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "email_required",
                Message = "Email is required"
            });
        }

        var (success, message, code) = await _emailVerificationService.ResendVerificationEmailAsync(request.Email);

        if (!success)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = code,
                Message = message
            });
        }

        return Ok(new AuthResponse
        {
            Success = true,
            Code = code,
            Message = message
        });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrEmpty(request.Email))
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "email_required",
                Message = "Email is required"
            });
        }

        var result = await _authService.ForgotPasswordAsync(request);
        return Ok(result);
    }

    [HttpPost("validate-reset-password-token")]
    public async Task<IActionResult> ValidateResetPasswordToken([FromBody] ValidateResetPasswordTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Invalid request data"
            });
        }

        var result = await _authService.ValidateResetPasswordTokenAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new AuthResponse
            {
                Success = false,
                Code = "invalid_request",
                Message = "Invalid request data"
            });
        }

        var result = await _authService.ResetPasswordAsync(request);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
