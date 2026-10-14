using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GamificationService.Application.Interfaces;

namespace GamificationService.Controllers;

[ApiController]
[Route("leaderboard")]
[Authorize]
/// <summary>
/// Публичная витрина leaderboard.
/// Глобальный и проектный топ читаются отдельно, потому что используют разные проекции и разные cache key.
/// </summary>
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardController(ILeaderboardService leaderboardService)
        => _leaderboardService = leaderboardService;

    [HttpGet("global")]
    /// <summary>
    /// Возвращает глобальный рейтинг пользователей по суммарному XP.
    /// </summary>
    public async Task<IActionResult> GetGlobalTop([FromQuery] int count = 10)
        => Ok(await _leaderboardService.GetGlobalTopAsync(Math.Min(count, 100)));

    [HttpGet("project/{projectId:guid}")]
    /// <summary>
    /// Возвращает рейтинг пользователей внутри конкретного проекта.
    /// </summary>
    public async Task<IActionResult> GetProjectTop(Guid projectId, [FromQuery] int count = 10)
        => Ok(await _leaderboardService.GetProjectTopAsync(projectId, Math.Min(count, 100)));
}
