using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Security;
using ManagementService.Application.Services;
using ManagementService.Contracts.Requests;
using ManagementService.Contracts.Responses;

namespace ManagementService.Controllers;

/// <summary>
/// API контроллер для управления статусами проектов
/// </summary>
[ApiController]
[Authorize]
public class StatusController : ControllerBase
{
    private readonly IStatusService _statusService;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<StatusController> _logger;

    public StatusController(IStatusService statusService, IRealtimeNotifier notifier, ILogger<StatusController> logger)
    {
        _statusService = statusService;
        _notifier = notifier;
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

    private ObjectResult ForbiddenWithCode(WorkspacePermissionException ex)
        => StatusCode(StatusCodes.Status403Forbidden, new { code = ex.Code, error = ex.Message });

    /// <summary>
    /// Получить все статусы проекта
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <returns>Список статусов</returns>
    [HttpGet("projects/{projectId}/statuses")]
    [ProducesResponseType(typeof(IEnumerable<ProjectStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ProjectStatusResponse>>> GetProjectStatuses([FromRoute] Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var statuses = await _statusService.GetProjectStatusesAsync(projectId, userId);

            var response = statuses.Select(s => new ProjectStatusResponse
            {
                Id = s.Id,
                Name = s.Name,
                Order = s.Order,
                Color = s.Color,
                IsFinal = s.IsFinal
            });

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting project statuses: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Создать новый статус в проекте
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="request">Данные для создания статуса</param>
    /// <returns>Созданный статус</returns>
    [HttpPost("projects/{projectId}/statuses")]
    [ProducesResponseType(typeof(ProjectStatusResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectStatusResponse>> CreateStatus(
        [FromRoute] Guid projectId, 
        [FromBody] CreateStatusRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var status = await _statusService.CreateStatusAsync(projectId, request.Name, request.Color, request.IsFinal, userId);
            _ = _notifier.StatusChanged(projectId);

            _logger.LogInformation($"User {userId} created status {status.Id} in project {projectId}");

            var response = new ProjectStatusResponse
            {
                Id = status.Id,
                Name = status.Name,
                Order = status.Order,
                Color = status.Color,
                IsFinal = status.IsFinal
            };

            return CreatedAtAction(nameof(GetStatus), new { statusId = status.Id }, response);
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("Forbidden access: {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating status: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить статус
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <param name="request">Данные для обновления</param>
    /// <returns>Обновленный статус</returns>
    [HttpPut("statuses/{statusId}")]
    [ProducesResponseType(typeof(ProjectStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProjectStatusResponse>> UpdateStatus(
        [FromRoute] Guid statusId, 
        [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var status = await _statusService.UpdateStatusAsync(statusId, request.Name, request.Color, request.IsFinal, userId);

            if (status == null)
            {
                return NotFound();
            }

            _ = _notifier.StatusChanged(status.ProjectId);
            _logger.LogInformation($"User {userId} updated status {statusId}");

            var response = new ProjectStatusResponse
            {
                Id = status.Id,
                Name = status.Name,
                Order = status.Order,
                Color = status.Color,
                IsFinal = status.IsFinal
            };

            return Ok(response);
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("Forbidden access: {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating status: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Удалить статус
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <returns>Результат операции</returns>
    [HttpDelete("statuses/{statusId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteStatus([FromRoute] Guid statusId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var status = await _statusService.GetStatusByIdAsync(statusId, userId);
            var deleted = await _statusService.DeleteStatusAsync(statusId, userId);

            if (!deleted)
            {
                return NotFound();
            }

            if (status != null)
            {
                _ = _notifier.StatusChanged(status.ProjectId);
            }
            _logger.LogInformation($"User {userId} deleted status {statusId}");
            return NoContent();
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("Forbidden access: {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Invalid operation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting status: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Изменить порядок статусов проекта
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="request">Новый порядок статусов</param>
    /// <returns>Результат операции</returns>
    [HttpPut("projects/{projectId}/statuses/reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReorderStatuses(
        [FromRoute] Guid projectId, 
        [FromBody] ReorderStatusesRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reordered = await _statusService.ReorderStatusesAsync(projectId, request.StatusIds, userId);

            if (!reordered)
            {
                return BadRequest(new { error = "Failed to reorder statuses" });
            }

            _ = _notifier.StatusChanged(projectId);
            _logger.LogInformation($"User {userId} reordered statuses in project {projectId}");
            return NoContent();
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("Forbidden access: {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning($"Invalid argument: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error reordering statuses: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить статус по ID
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <returns>Статус</returns>
    [HttpGet("statuses/{statusId}")]
    [ProducesResponseType(typeof(ProjectStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProjectStatusResponse>> GetStatus([FromRoute] Guid statusId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var status = await _statusService.GetStatusByIdAsync(statusId, userId);

            if (status == null)
            {
                return NotFound();
            }

            var response = new ProjectStatusResponse
            {
                Id = status.Id,
                Name = status.Name,
                Order = status.Order,
                Color = status.Color,
                IsFinal = status.IsFinal
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting status: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
