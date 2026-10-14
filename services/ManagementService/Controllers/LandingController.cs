using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Landing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementService.Controllers;

[ApiController]
[AllowAnonymous]
[Route("landing")]
public sealed class LandingController : ControllerBase
{
    private readonly ILandingContentService _landingContentService;

    public LandingController(ILandingContentService landingContentService)
    {
        _landingContentService = landingContentService;
    }

    [HttpGet("{locale}")]
    public async Task<ActionResult<LandingContentDocument>> GetLocale(
        string locale,
        CancellationToken cancellationToken = default)
    {
        if (!LandingLocales.IsSupported(locale))
        {
            return BadRequest(new { code = "unsupported_locale", message = "Only en, ru and kk locales are supported." });
        }

        return Ok(await _landingContentService.GetPublicAsync(locale, cancellationToken));
    }
}
