using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Application.Interfaces;
using UserService.Contracts.Requests;
using UserService.Infrastructure.Data;

namespace UserService.Controllers;

[ApiController]
[Route("")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserMeService _userMeService;
    private readonly IValidator<UpdateProfileRequest> _updateProfileValidator;
    private readonly IValidator<UpdatePreferencesRequest> _updatePreferencesValidator;
    private readonly IValidator<UpdateOnboardingRequest> _updateOnboardingValidator;
    private readonly IValidator<UpdateActivityRequest> _updateActivityValidator;

    public UsersController(
        IUserMeService userMeService,
        IValidator<UpdateProfileRequest> updateProfileValidator,
        IValidator<UpdatePreferencesRequest> updatePreferencesValidator,
        IValidator<UpdateOnboardingRequest> updateOnboardingValidator,
        IValidator<UpdateActivityRequest> updateActivityValidator)
    {
        _userMeService = userMeService;
        _updateProfileValidator = updateProfileValidator;
        _updatePreferencesValidator = updatePreferencesValidator;
        _updateOnboardingValidator = updateOnboardingValidator;
        _updateActivityValidator = updateActivityValidator;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.GetMeAsync(userId, GetUsernameFallback(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("me/profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.GetProfileAsync(userId, GetUsernameFallback(), cancellationToken);
        return Ok(response);
    }

    [HttpPatch("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var validation = await _updateProfileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        var response = await _userMeService.UpdateProfileAsync(userId, request, GetUsernameFallback(), cancellationToken);
        return Ok(response);
    }

    [HttpGet("me/preferences")]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.GetPreferencesAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var validation = await _updatePreferencesValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        var response = await _userMeService.UpdatePreferencesAsync(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpGet("me/onboarding")]
    public async Task<IActionResult> GetOnboarding(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.GetOnboardingAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("me/onboarding")]
    public async Task<IActionResult> UpdateOnboarding([FromBody] UpdateOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var validation = await _updateOnboardingValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        var response = await _userMeService.UpdateOnboardingAsync(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("me/onboarding/complete")]
    public async Task<IActionResult> CompleteOnboarding(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.CompleteOnboardingAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("me/activity/ping")]
    public async Task<IActionResult> PingActivity(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var response = await _userMeService.PingActivityAsync(userId, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("me/activity")]
    public async Task<IActionResult> UpdateActivity([FromBody] UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return UnauthorizedResponse();
        }

        var validation = await _updateActivityValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        var response = await _userMeService.UpdateActivityAsync(userId, request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("public/batch")]
    public async Task<IActionResult> GetPublicBatch([FromBody] List<Guid> userIds, CancellationToken cancellationToken)
    {
        var sanitized = (userIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (sanitized.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var response = await _userMeService.GetPublicProfilesAsync(sanitized, cancellationToken);
        return Ok(response);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new
            {
                success = false,
                message = "Query parameter 'q' is required"
            });
        }

        var response = await _userMeService.SearchUsersAsync(q, limit, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("health")]
    public async Task<IActionResult> Health([FromServices] UserDbContext db)
    {
        try
        {
            await db.Database.CanConnectAsync();
            return Ok(new { status = "healthy", service = "user" });
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy", service = "user" });
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out userId))
        {
            return true;
        }

        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(nameIdentifier, out userId);
    }

    private string? GetUsernameFallback()
    {
        return User.FindFirst("preferred_username")?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value;
    }

    private IActionResult UnauthorizedResponse()
    {
        return Unauthorized(new
        {
            success = false,
            message = "Unauthorized"
        });
    }

    private IActionResult ValidationProblem(FluentValidation.Results.ValidationResult validation)
    {
        var errors = validation.Errors
            .GroupBy(x => x.PropertyName)
            .ToDictionary(
                x => x.Key,
                x => x.Select(e => e.ErrorMessage).Distinct().ToArray());

        return BadRequest(new
        {
            success = false,
            message = "Validation failed",
            errors
        });
    }
}

