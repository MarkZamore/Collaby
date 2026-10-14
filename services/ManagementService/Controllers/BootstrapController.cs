using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;

namespace ManagementService.Controllers;

/// <summary>
/// Агрегирующий endpoint для начальной загрузки приложения.
/// Один запрос — все базовые данные для фронтенда.
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize]
public class BootstrapController : ControllerBase
{
    private readonly IBootstrapService _bootstrapService;
    private readonly ILogger<BootstrapController> _logger;

    public BootstrapController(IBootstrapService bootstrapService, ILogger<BootstrapController> logger)
    {
        _bootstrapService = bootstrapService;
        _logger = logger;
    }

    /// <summary>
    /// Получить ID текущего пользователя из JWT токена
    /// </summary>
    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        }

        return userId;
    }

    /// <summary>
    /// GET /bootstrap — агрегированные данные для начальной загрузки.
    /// Возвращает: профиль, workspaces с проектами, участников, pending invites.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(BootstrapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BootstrapResponse>> GetBootstrapData(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var data = await _bootstrapService.GetBootstrapDataAsync(userId, ct);

            _logger.LogInformation("Bootstrap loaded for user {UserId}: {WorkspaceCount} workspaces, partial={Partial}",
                userId, data.Workspaces.Count, data.Partial);

            return Ok(data);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bootstrap failed");
            return StatusCode(500, new { error = "Bootstrap failed" });
        }
    }
}
