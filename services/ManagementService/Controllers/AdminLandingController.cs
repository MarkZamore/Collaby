using System.Security.Claims;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Landing;
using ManagementService.Contracts.Responses.Admin;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementService.Controllers;

[ApiController]
[Authorize(Policy = SystemAdminConstants.PolicyName)]
[Route("admin/landing")]
public sealed class AdminLandingController : ControllerBase
{
    private readonly ILandingContentService _landingContentService;

    public AdminLandingController(ILandingContentService landingContentService)
    {
        _landingContentService = landingContentService;
    }

    [HttpGet("{locale}")]
    public async Task<ActionResult<AdminLandingLocaleContentResponse>> GetLocale(
        string locale,
        CancellationToken cancellationToken = default)
    {
        if (!LandingLocales.IsSupported(locale))
        {
            return BadRequest(new { code = "unsupported_locale", message = "Only en, ru and kk locales are supported." });
        }

        return Ok(await _landingContentService.GetAdminAsync(locale, cancellationToken));
    }

    [HttpPut("{locale}")]
    public async Task<ActionResult<AdminLandingLocaleContentResponse>> SaveLocale(
        string locale,
        [FromBody] LandingContentDocument content,
        CancellationToken cancellationToken = default)
    {
        if (!LandingLocales.IsSupported(locale))
        {
            return BadRequest(new { code = "unsupported_locale", message = "Only en, ru and kk locales are supported." });
        }

        var response = await _landingContentService.SaveAsync(
            locale,
            content ?? new LandingContentDocument(),
            GetCurrentUserId(),
            GetCurrentUserEmail(),
            cancellationToken);

        return Ok(response);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private string? GetCurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
    }
}
