using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Security;
using ManagementService.Application.Services;
using ManagementService.Contracts.Requests;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Enums;

namespace ManagementService.Controllers;

/// <summary>
/// API контроллер для управления рабочими пространствами
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IRealtimeNotifier _notifier;
    private readonly INotificationService _notifications;
    private readonly ILogger<WorkspacesController> _logger;

    public WorkspacesController(IWorkspaceService workspaceService, IRealtimeNotifier notifier, INotificationService notifications, ILogger<WorkspacesController> logger)
    {
        _workspaceService = workspaceService;
        _notifier = notifier;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>
    /// Получить ID текущего пользователя из JWT токена
    /// </summary>
    private Guid GetCurrentUserId()
    {
        // ASP.NET Core maps "sub" to ClaimTypes.NameIdentifier by default
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
    /// Создать новое рабочее пространство
    /// </summary>
    /// <param name="request">Данные для создания рабочего пространства</param>
    /// <returns>Созданное рабочее пространство</returns>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WorkspaceResponse>> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var workspace = await _workspaceService.CreateWorkspaceAsync(request.Name, request.Description, userId);

            _logger.LogInformation($"User {userId} created workspace {workspace.Id}");

            return CreatedAtAction(nameof(GetWorkspaceById), new { id = workspace.Id }, workspace);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating workspace: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить список всех рабочих пространств текущего пользователя
    /// </summary>
    /// <returns>Список рабочих пространств</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkspaceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<WorkspaceResponse>>> GetWorkspaces()
    {
        try
        {
            var userId = GetCurrentUserId();

            var workspaces = await _workspaceService.GetUserWorkspacesAsync(userId);

            _logger.LogInformation($"User {userId} retrieved {workspaces.Count()} workspaces");

            return Ok(workspaces);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting workspaces: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить конкретное рабочее пространство по ID
    /// </summary>
    /// <param name="id">ID рабочего пространства</param>
    /// <returns>Рабочее пространство</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceResponse>> GetWorkspaceById([FromRoute] Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();

            var workspace = await _workspaceService.GetWorkspaceByIdAsync(id, userId);

            if (workspace == null)
            {
                _logger.LogWarning($"User {userId} tried to access non-existent or unauthorized workspace {id}");
                return NotFound(new { error = "Workspace not found or access denied" });
            }

            _logger.LogInformation($"User {userId} retrieved workspace {id}");

            return Ok(workspace);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting workspace: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получить участников workspace
    /// </summary>
    /// <param name="id">ID рабочего пространства</param>
    /// <returns>Список участников</returns>
    [HttpGet("{id}/members")]
    [ProducesResponseType(typeof(IEnumerable<WorkspaceMemberResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _workspaceService.GetWorkspaceMembersEnrichedAsync(id, userId, HttpContext.RequestAborted);

            _logger.LogInformation($"User {userId} retrieved {response.Count} members from workspace {id}");

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting workspace members: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Добавить участника в workspace
    /// </summary>
    /// <param name="id">ID рабочего пространства</param>
    /// <param name="request">Данные участника</param>
    /// <returns>Созданный участник</returns>
    [HttpPost("{id}/members")]
    [ProducesResponseType(typeof(WorkspaceMemberResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var addResult = await _workspaceService.AddMemberAsync(id, request.UserId, request.Role, currentUserId);
            var member = addResult.Member;

            var response = new WorkspaceMemberResponse
            {
                UserId = member.UserId,
                WorkspaceId = member.WorkspaceId,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                IsActive = member.Status == "Active"
            };

            _logger.LogInformation($"User {currentUserId} added user {request.UserId} to workspace {id}");

            await _notifier.MemberChanged(id);
            await _notifier.WorkspaceChanged(id);

            // Send in-app notification to the invited user
            if (request.UserId != currentUserId && addResult.ShouldSendInviteNotification)
            {
                var workspace = await _workspaceService.GetWorkspaceByIdAsync(id, currentUserId);
                await _notifications.CreateAsync(
                    request.UserId,
                    NotificationType.WorkspaceInvite,
                    "Приглашение в рабочее пространство",
                    workspace?.Name ?? "Рабочее пространство",
                    referenceId: id,
                    actorId: currentUserId);
            }

            return CreatedAtAction(nameof(GetMembers), new { id }, response);
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("Forbidden access: {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Invalid operation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding member: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Обновить роль участника
    /// </summary>
    /// <param name="id">ID рабочего пространства</param>
    /// <param name="userId">ID пользователя</param>
    /// <param name="request">Новая роль</param>
    /// <returns>Обновленный участник</returns>
    [HttpPut("{id}/members/{userId}")]
    [ProducesResponseType(typeof(WorkspaceMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleRequest request)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var member = await _workspaceService.UpdateMemberRoleAsync(id, userId, request.Role, currentUserId);

            if (member == null)
            {
                return NotFound(new { error = "Member not found" });
            }

            var response = new WorkspaceMemberResponse
            {
                UserId = member.UserId,
                WorkspaceId = member.WorkspaceId,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                IsActive = member.Status == "Active"
            };

            _logger.LogInformation($"User {currentUserId} updated role of user {userId} in workspace {id}");

            _ = _notifier.MemberChanged(id);
            _ = _notifier.WorkspaceChanged(id);
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
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Invalid operation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating member role: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Удалить участника из workspace
    /// </summary>
    /// <param name="id">ID рабочего пространства</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Результат удаления</returns>
    [HttpDelete("{id}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var result = await _workspaceService.RemoveMemberAsync(id, userId, currentUserId);

            if (!result)
            {
                return NotFound(new { error = "Member not found" });
            }

            _logger.LogInformation($"User {currentUserId} removed user {userId} from workspace {id}");

            _ = _notifier.MemberChanged(id);
            _ = _notifier.WorkspaceChanged(id);
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
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Invalid operation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error removing member: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Удалить рабочее пространство
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var result = await _workspaceService.DeleteWorkspaceAsync(id, currentUserId);
            if (!result) return NotFound(new { error = "Workspace not found" });
            _logger.LogInformation($"User {currentUserId} deleted workspace {id}");
            return NoContent();
        }
        catch (WorkspacePermissionException ex)
        {
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting workspace: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
