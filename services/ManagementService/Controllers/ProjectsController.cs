using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Security;
using ManagementService.Application.Services;
using ManagementService.Contracts.Requests;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;

namespace ManagementService.Controllers;

/// <summary>
/// API контроллер для управления проектами
/// </summary>
[ApiController]
[Route("workspaces/{workspaceId}/projects")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, IRealtimeNotifier notifier, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _notifier = notifier;
        _logger = logger;
    }

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

    internal static ProjectResponse MapToProjectResponse(Project project)
    {
        return new ProjectResponse
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            WorkspaceId = project.WorkspaceId,
            CreatedById = project.CreatedBy,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt,
            IsArchived = project.IsArchived,
            BoardStickerIds = !string.IsNullOrEmpty(project.BoardStickerIds)
                ? JsonSerializer.Deserialize<List<string>>(project.BoardStickerIds)
                : null,
            Statuses = project.Statuses.Select(s => new ProjectStatusResponse
            {
                Id = s.Id,
                Name = s.Name,
                Order = s.Order,
                Color = s.Color,
                IsFinal = s.IsFinal
            }).ToList()
        };
    }

    private ObjectResult ForbiddenWithCode(WorkspacePermissionException ex)
        => StatusCode(StatusCodes.Status403Forbidden, new { code = ex.Code, error = ex.Message });

    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> CreateProject(
        [FromRoute] Guid workspaceId, 
        [FromBody] CreateProjectRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.CreateProjectAsync(workspaceId, request.Name, request.Description, userId);
            _ = _notifier.ProjectChanged(workspaceId);
            _logger.LogInformation("User {UserId} created project {ProjectId} in workspace {WorkspaceId}", userId, project.Id, workspaceId);
            return CreatedAtAction(nameof(GetProjectById), 
                new { workspaceId = project.WorkspaceId, projectId = project.Id }, 
                MapToProjectResponse(project));
        }
        catch (WorkspacePermissionException ex) { return ForbiddenWithCode(ex); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectResponse>>> GetProjectsByWorkspace([FromRoute] Guid workspaceId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var projects = await _projectService.GetProjectsByWorkspaceAsync(workspaceId, userId);
            return Ok(projects.Select(MapToProjectResponse));
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{projectId}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProjectById(
        [FromRoute] Guid workspaceId, 
        [FromRoute] Guid projectId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.GetProjectByIdAsync(projectId, userId);
            if (project == null) return NotFound();
            return Ok(MapToProjectResponse(project));
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }
}

/// <summary>
/// API контроллер для работы с проектами напрямую (GET /projects/{id}, PUT board-stickers)
/// </summary>
[ApiController]
[Route("projects")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(IProjectService projectService, IRealtimeNotifier notifier, ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _notifier = notifier;
        _logger = logger;
    }

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
    /// Получить проект по ID (прямой доступ)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetProject([FromRoute] Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.GetProjectByIdAsync(id, userId);
            if (project == null) return NotFound();
            return Ok(ProjectsController.MapToProjectResponse(project));
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Обновить список стикеров доски проекта
    /// </summary>
    [HttpPut("{projectId}/board-stickers")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> UpdateBoardStickers(
        [FromRoute] Guid projectId, 
        [FromBody] UpdateBoardStickersRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var project = await _projectService.UpdateBoardStickerIdsAsync(projectId, request.BoardStickerIds, userId);
            if (project == null) return NotFound();
            _ = _notifier.ProjectChanged(project.WorkspaceId);
            _logger.LogInformation("User {UserId} updated board stickers for project {ProjectId}", userId, projectId);
            return Ok(ProjectsController.MapToProjectResponse(project));
        }
        catch (WorkspacePermissionException ex) { return ForbiddenWithCode(ex); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex) { return BadRequest(new { error = ex.Message }); }
    }

    /// <summary>
    /// Удалить проект
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject([FromRoute] Guid id)
    {
        var userId = GetCurrentUserId();
        _logger.LogInformation("[DeleteProject] User {UserId} attempting to delete project {ProjectId}", userId, id);
        
        try
        {
            var projectBeforeDelete = await _projectService.GetProjectByIdAsync(id, userId);
            var result = await _projectService.DeleteProjectAsync(id, userId);
            if (!result)
            {
                _logger.LogWarning("[DeleteProject] Project {ProjectId} not found", id);
                return NotFound(new { error = "Project not found" });
            }
            if (projectBeforeDelete != null)
            {
                _ = _notifier.ProjectChanged(projectBeforeDelete.WorkspaceId);
            }
            _logger.LogInformation("[DeleteProject] User {UserId} successfully deleted project {ProjectId}", userId, id);
            return NoContent();
        }
        catch (WorkspacePermissionException ex)
        {
            _logger.LogWarning("[DeleteProject] Forbidden - {Code} {Message}", ex.Code, ex.Message);
            return ForbiddenWithCode(ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("[DeleteProject] Unauthorized - {Message}", ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError("[DeleteProject] Error deleting project {ProjectId}: {Message}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
