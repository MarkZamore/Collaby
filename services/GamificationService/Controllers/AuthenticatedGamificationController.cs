using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace GamificationService.Controllers;

/// <summary>
/// Базовый контроллер для публичных gamification endpoint-ов.
/// Централизует извлечение текущего пользователя из JWT, чтобы доменные контроллеры
/// не дублировали auth-обвязку в каждом методе.
/// </summary>
public abstract class AuthenticatedGamificationController : ControllerBase
{
    protected bool TryGetUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out userId);
    }

    protected ActionResult<Guid> GetUserIdOrUnauthorized()
    {
        if (TryGetUserId(out var userId))
        {
            return userId;
        }

        return Unauthorized(new { error = "User id claim is missing." });
    }
}
