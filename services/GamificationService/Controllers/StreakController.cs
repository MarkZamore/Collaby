using GamificationService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamificationService.Controllers;

[ApiController]
[Route("streak")]
[Authorize]
/// <summary>
/// Пользовательский API streak-механики: просмотр текущей серии и активация freeze.
/// </summary>
public class StreakController : AuthenticatedGamificationController
{
    private readonly IStreakService _streakService;

    public StreakController(IStreakService streakService) => _streakService = streakService;

    [HttpGet]
    public async Task<IActionResult> GetStreak()
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _streakService.GetStreakAsync(userIdResult.Value));
    }

    [HttpPost("freeze")]
    public async Task<IActionResult> UseFreeze()
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var success = await _streakService.UseFreezeAsync(userIdResult.Value);
        if (!success)
        {
            return BadRequest(new { error = "No freeze items available" });
        }

        return Ok(new { message = "Freeze activated" });
    }
}
