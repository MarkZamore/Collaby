using GamificationService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GamificationService.Controllers;

[ApiController]
[Route("quests")]
[Authorize]
/// <summary>
/// Пользовательский API для чтения активных квестов и получения награды за завершённый квест.
/// </summary>
public class QuestController : AuthenticatedGamificationController
{
    private readonly IQuestService _questService;

    public QuestController(IQuestService questService) => _questService = questService;

    [HttpGet]
    public async Task<IActionResult> GetQuests()
    {
        var userIdResult = GetUserIdOrUnauthorized();
        return userIdResult.Result ?? Ok(await _questService.GetActiveQuestsAsync(userIdResult.Value));
    }

    [HttpPost("{questId:guid}/claim")]
    public async Task<IActionResult> ClaimQuest(Guid questId)
    {
        var userIdResult = GetUserIdOrUnauthorized();
        if (userIdResult.Result is not null)
        {
            return userIdResult.Result;
        }

        var result = await _questService.ClaimQuestAsync(userIdResult.Value, questId);
        if (result == null)
        {
            return BadRequest(new { error = "Quest not completed or not found" });
        }

        return Ok(result);
    }
}
