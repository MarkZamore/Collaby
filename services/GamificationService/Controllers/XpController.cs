using GamificationService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamificationService.Controllers;

[ApiController]
[Route("xp")]
[Authorize]
/// <summary>
/// Публичный API для просмотра глобального XP, проектного XP и истории транзакций пользователя.
/// </summary>
public class XpController : AuthenticatedGamificationController
{
    private readonly IXpService _xpService;

    public XpController(IXpService xpService) => _xpService = xpService;

    [HttpGet("global")]
    public async Task<IActionResult> GetGlobalXp()
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _xpService.GetUserGlobalXpAsync(userIdResult.Value));
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<IActionResult> GetProjectXp(Guid projectId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _xpService.GetUserProjectXpAsync(userIdResult.Value, projectId));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _xpService.GetXpHistoryAsync(userIdResult.Value, limit, offset));
    }
}
