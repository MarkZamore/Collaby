using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;

namespace ManagementService.Controllers;

/// <summary>
/// API текущего пользователя: входящие, мои задачи и т.д.
/// </summary>
[ApiController]
[Route("me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<MeController> _logger;

    public MeController(ITaskService taskService, ILogger<MeController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        return userId;
    }

    /// <summary>
    /// GET /me/inbox — задачи «к выполнению», назначенные текущему пользователю, по всем воркспейсам.
    /// </summary>
    [HttpGet("inbox")]
    [ProducesResponseType(typeof(IEnumerable<InboxItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<InboxItemResponse>>> GetInbox(CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var items = await _taskService.GetInboxAsync(userId, ct);
            return Ok(items);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetInbox failed");
            return StatusCode(500, new { error = "Failed to load inbox" });
        }
    }
}
