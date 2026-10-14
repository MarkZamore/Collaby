using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MessengerService.Application.Interfaces;
using MessengerService.Contracts.Responses;

namespace MessengerService.Controllers;

[ApiController]
[Route("presence")]
[Authorize]
public class PresenceController : ControllerBase
{
    private readonly IPresenceService _presenceService;

    public PresenceController(IPresenceService presenceService)
    {
        _presenceService = presenceService;
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(IReadOnlyList<PresenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PresenceResponse>>> GetPresenceBatch([FromBody] List<Guid> userIds)
    {
        var presences = await _presenceService.GetPresenceAsync(userIds);
        return Ok(presences);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        return userId;
    }
}
