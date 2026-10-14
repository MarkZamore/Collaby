using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Requests;
using ManagementService.Contracts.Responses;

namespace ManagementService.Controllers;

/// <summary>
/// API контроллер для управления приглашениями в workspace
/// </summary>
[ApiController]
[Route("Workspaces/{workspaceId}/invitations")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class InvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly ILogger<InvitationsController> _logger;

    public InvitationsController(
        IInvitationService invitationService,
        ILogger<InvitationsController> _logger)
    {
        _invitationService = invitationService;
        this._logger = _logger;
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

    private Guid? TryGetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return userId;
    }

    /// <summary>
    /// Получить все приглашения workspace
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetInvitations(Guid workspaceId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var invitations = await _invitationService.GetWorkspaceInvitationsAsync(workspaceId, userId);

            var response = invitations.Select(i => new InvitationResponse
            {
                Id = i.Id,
                WorkspaceId = i.WorkspaceId,
                Email = i.Email,
                WorkspaceName = i.Workspace?.Name,
                Role = i.Role,
                InvitedBy = i.InvitedBy,
                CreatedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                AcceptedAt = i.AcceptedAt,
                AcceptedBy = i.AcceptedBy,
                Status = i.Status,
                CanAccept = i.Status == "Pending"
            });

            _logger.LogInformation($"User {userId} retrieved {invitations.Count()} invitations from workspace {workspaceId}");

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning($"Unauthorized access: {ex.Message}");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting invitations: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Создать приглашение
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvitation(
        Guid workspaceId,
        [FromBody] CreateInvitationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var invitation = await _invitationService.CreateInvitationAsync(
                workspaceId,
                request.Email,
                request.Role,
                request.ExpiresInDays,
                userId
            );

            var response = new InvitationResponse
            {
                Id = invitation.Id,
                WorkspaceId = invitation.WorkspaceId,
                Email = invitation.Email,
                WorkspaceName = invitation.Workspace?.Name,
                Role = invitation.Role,
                Token = invitation.Token, // Возвращаем токен только при создании
                InvitedBy = invitation.InvitedBy,
                CreatedAt = invitation.CreatedAt,
                ExpiresAt = invitation.ExpiresAt,
                Status = invitation.Status,
                CanAccept = invitation.Status == "Pending"
            };

            _logger.LogInformation($"User {userId} created invitation for {request.Email} to workspace {workspaceId}");

            return CreatedAtAction(nameof(GetInvitations), new { workspaceId }, response);
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
            _logger.LogError($"Error creating invitation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отозвать приглашение
    /// </summary>
    [HttpDelete("{invitationId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvitation(Guid workspaceId, Guid invitationId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _invitationService.RevokeInvitationAsync(invitationId, userId);

            if (!result)
            {
                return NotFound(new { error = "Invitation not found" });
            }

            _logger.LogInformation($"User {userId} revoked invitation {invitationId}");

            return NoContent();
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
            _logger.LogError($"Error revoking invitation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}

/// <summary>
/// API контроллер для принятия приглашений (не требует авторизации для просмотра)
/// </summary>
[ApiController]
[Route("invitations")]
public class PublicInvitationsController : ControllerBase
{
    private readonly IInvitationService _invitationService;
    private readonly ILogger<PublicInvitationsController> _logger;

    public PublicInvitationsController(
        IInvitationService invitationService,
        ILogger<PublicInvitationsController> logger)
    {
        _invitationService = invitationService;
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

    private Guid? TryGetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return null;
        }

        return userId;
    }

    /// <summary>
    /// Получить информацию о приглашении по токену (для preview)
    /// </summary>
    [HttpGet("{token}")]
    [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitationByToken(string token)
    {
        try
        {
            var invitation = await _invitationService.GetInvitationByTokenAsync(token);

            if (invitation == null)
            {
                return NotFound(new { error = "Invitation not found" });
            }

            var viewerUserId = TryGetCurrentUserId();
            var alreadyMember = viewerUserId.HasValue
                && await _invitationService.IsInvitationAlreadySatisfiedByUserAsync(
                    invitation.WorkspaceId,
                    invitation.Email,
                    viewerUserId.Value);

            var response = new InvitationResponse
            {
                Id = invitation.Id,
                WorkspaceId = invitation.WorkspaceId,
                Email = invitation.Email,
                WorkspaceName = invitation.Workspace?.Name,
                Role = invitation.Role,
                // НЕ возвращаем токен в публичном API
                InvitedBy = invitation.InvitedBy,
                CreatedAt = invitation.CreatedAt,
                ExpiresAt = invitation.ExpiresAt,
                AcceptedAt = invitation.AcceptedAt,
                AcceptedBy = invitation.AcceptedBy,
                Status = invitation.Status,
                AlreadyMember = alreadyMember,
                CanAccept = invitation.Status == "Pending" && !alreadyMember
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting invitation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Принять приглашение (требует авторизацию)
    /// </summary>
    [HttpPost("accept")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ProducesResponseType(typeof(WorkspaceMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AcceptInvitation([FromBody] AcceptInvitationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var member = await _invitationService.AcceptInvitationAsync(request.Token, userId);

            var response = new WorkspaceMemberResponse
            {
                UserId = member.UserId,
                WorkspaceId = member.WorkspaceId,
                Role = member.Role,
                JoinedAt = member.JoinedAt,
                IsActive = member.Status == "Active"
            };

            _logger.LogInformation($"User {userId} accepted invitation and joined workspace {member.WorkspaceId}");

            return Ok(response);
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
            _logger.LogError($"Error accepting invitation: {ex.Message}");
            return BadRequest(new { error = ex.Message });
        }
    }
}
