using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.InternalAuth;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ManagementService.Controllers;

[ApiController]
[Route("internal/v1/queries/projects")]
public sealed class InternalProjectsController : ControllerBase
{
    private readonly ManagementDbContext _dbContext;
    private readonly IProjectProgressSnapshotBuilder _progressBuilder;
    private readonly InternalServiceRequestAuthenticator _internalRequestAuthenticator;

    public InternalProjectsController(
        ManagementDbContext dbContext,
        IProjectProgressSnapshotBuilder progressBuilder,
        InternalServiceRequestAuthenticator internalRequestAuthenticator)
    {
        _dbContext = dbContext;
        _progressBuilder = progressBuilder;
        _internalRequestAuthenticator = internalRequestAuthenticator;
    }

    [HttpGet("{projectId:guid}/progress")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectProgress(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        var response = await _progressBuilder.BuildAsync(projectId, cancellationToken);
        if (response is null)
        {
            return NotFound(new { success = false, message = "Project not found" });
        }

        return Ok(response);
    }

    [HttpGet("{projectId:guid}/memberships/{userId:guid}/owner-or-admin")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOwnerOrAdminAccess(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .Where(entry => entry.Id == projectId)
            .Select(entry => new { entry.Id, entry.WorkspaceId })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return NotFound(new { success = false, message = "Project not found" });
        }

        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(entry => entry.Id == project.WorkspaceId)
            .Select(entry => new { entry.Id, entry.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return NotFound(new { success = false, message = "Workspace not found" });
        }

        var role = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member =>
                member.WorkspaceId == workspace.Id &&
                member.UserId == userId &&
                member.Status == "Active")
            .Select(member => member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        var isOwnerOrAdmin = workspace.OwnerId == userId ||
            string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            ProjectId = project.Id,
            WorkspaceId = workspace.Id,
            IsOwnerOrAdmin = isOwnerOrAdmin
        });
    }

    [HttpGet("{projectId:guid}/analytics-snapshot")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAnalyticsSnapshot(
        Guid projectId,
        [FromQuery] Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        if (userId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "Query parameter 'userId' is required" });
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .Where(entry => entry.Id == projectId)
            .Select(entry => new { entry.Id, entry.WorkspaceId })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return NotFound(new { success = false, message = "Project not found" });
        }

        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(entry => entry.Id == project.WorkspaceId)
            .Select(entry => new { entry.Id, entry.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return NotFound(new { success = false, message = "Workspace not found" });
        }

        var activeMembers = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspace.Id && member.Status == "Active")
            .OrderBy(member => member.JoinedAt)
            .Select(member => new AnalyticsSnapshotMemberDto
            {
                UserId = member.UserId,
                Role = member.Role
            })
            .ToListAsync(cancellationToken);

        if (activeMembers.All(member => member.UserId != workspace.OwnerId))
        {
            activeMembers.Insert(0, new AnalyticsSnapshotMemberDto
            {
                UserId = workspace.OwnerId,
                Role = "Owner"
            });
        }

        var hasAccess = workspace.OwnerId == userId ||
            activeMembers.Any(member => member.UserId == userId);

        var taskCountsByStatus = await _dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == project.Id && !task.IsArchived && task.StatusId != null)
            .GroupBy(task => task.StatusId!.Value)
            .Select(group => new { StatusId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.StatusId, entry => entry.Count, cancellationToken);

        var statusDistribution = await _dbContext.ProjectStatuses
            .AsNoTracking()
            .Where(status => status.ProjectId == project.Id)
            .OrderBy(status => status.Order)
            .Select(status => new AnalyticsSnapshotStatusDistributionDto
            {
                StatusId = status.Id,
                Name = status.Name,
                Color = status.Color,
                Order = status.Order,
                IsFinal = status.IsFinal,
                TaskCount = 0
            })
            .ToListAsync(cancellationToken);

        foreach (var status in statusDistribution)
        {
            status.TaskCount = taskCountsByStatus.GetValueOrDefault(status.StatusId);
        }

        return Ok(new AnalyticsSnapshotResponse
        {
            ProjectId = project.Id,
            WorkspaceId = workspace.Id,
            HasAccess = hasAccess,
            StatusDistribution = statusDistribution,
            Members = activeMembers
        });
    }

    [HttpGet("{projectId:guid}/assistant-snapshot")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAssistantSnapshot(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        var project = await _dbContext.Projects
            .AsNoTracking()
            .Where(entry => entry.Id == projectId)
            .Select(entry => new AssistantSnapshotProjectDto
            {
                Id = entry.Id,
                WorkspaceId = entry.WorkspaceId,
                Name = entry.Name,
                Description = entry.Description,
                CreatedBy = entry.CreatedBy,
                BoardStickerIds = ParseBoardStickerIds(entry.BoardStickerIds)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return NotFound(new { success = false, message = "Project not found" });
        }

        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(entry => entry.Id == project.WorkspaceId)
            .Select(entry => new AssistantSnapshotWorkspaceDto
            {
                Id = entry.Id,
                Name = entry.Name,
                Description = entry.Description,
                OwnerId = entry.OwnerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return NotFound(new { success = false, message = "Workspace not found" });
        }

        var members = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspace.Id && member.Status == "Active")
            .OrderBy(member => member.JoinedAt)
            .Select(member => new AssistantSnapshotMemberDto
            {
                UserId = member.UserId,
                Role = member.Role,
                Status = member.Status,
                JoinedAt = member.JoinedAt
            })
            .ToListAsync(cancellationToken);

        var statuses = await _dbContext.ProjectStatuses
            .AsNoTracking()
            .Where(status => status.ProjectId == project.Id)
            .OrderBy(status => status.Order)
            .Select(status => new AssistantSnapshotStatusDto
            {
                Id = status.Id,
                Name = status.Name,
                Order = status.Order,
                Color = status.Color,
                IsFinal = status.IsFinal
            })
            .ToListAsync(cancellationToken);

        var labels = await _dbContext.Set<ProjectLabel>()
            .AsNoTracking()
            .Where(label => label.ProjectId == project.Id)
            .OrderBy(label => label.Order)
            .Select(label => new AssistantSnapshotLabelDto
            {
                Id = label.Id,
                Name = label.Name,
                Color = label.Color
            })
            .ToListAsync(cancellationToken);

        var tasks = await _dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == project.Id && !task.IsArchived)
            .OrderBy(task => task.Order)
            .Select(task => new AssistantSnapshotTaskDto
            {
                Id = task.Id,
                StatusId = task.StatusId,
                StatusName = task.Status != null ? task.Status.Name : null,
                Title = task.Title,
                Description = task.Description,
                AssigneeId = task.AssigneeId,
                CreatedBy = task.CreatedBy,
                Priority = task.Priority.ToString(),
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                Labels = task.LabelAssignments
                    .Where(assignment => assignment.Label != null)
                    .Select(assignment => new AssistantSnapshotLabelDto
                    {
                        Id = assignment.Label!.Id,
                        Name = assignment.Label.Name,
                        Color = assignment.Label.Color
                    })
                    .ToList(),
                SubTasks = task.SubTasks
                    .OrderBy(subTask => subTask.Order)
                    .Select(subTask => new AssistantSnapshotSubTaskDto
                    {
                        Id = subTask.Id,
                        Title = subTask.Title,
                        IsCompleted = subTask.IsCompleted,
                        Order = subTask.Order,
                        CreatedAt = subTask.CreatedAt
                    })
                    .ToList(),
                Comments = task.Comments
                    .OrderBy(comment => comment.CreatedAt)
                    .Select(comment => new AssistantSnapshotCommentDto
                    {
                        Id = comment.Id,
                        AuthorId = comment.AuthorId,
                        Text = comment.Text,
                        CreatedAt = comment.CreatedAt,
                        UpdatedAt = comment.UpdatedAt
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return Ok(new AssistantProjectSnapshotResponse
        {
            Workspace = workspace,
            Project = project,
            Members = members,
            Statuses = statuses,
            Labels = labels,
            Tasks = tasks
        });
    }

    [HttpGet("progress/export")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportProgress(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        var normalizedLimit = Math.Clamp(limit, 1, 500);
        var offset = DecodeCursor(cursor);

        var projectIds = await _dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Id)
            .Select(project => project.Id)
            .Skip(offset)
            .Take(normalizedLimit + 1)
            .ToListAsync(cancellationToken);

        var pageIds = projectIds.Take(normalizedLimit).ToList();
        var items = new List<ManagementProjectProgressSnapshotEventV1>(pageIds.Count);

        foreach (var projectId in pageIds)
        {
            var progress = await _progressBuilder.BuildAsync(projectId, cancellationToken);
            if (progress is null)
            {
                continue;
            }

            items.Add(new ManagementProjectProgressSnapshotEventV1(
                progress.ProjectId,
                progress.ProgressPercent,
                progress.TotalWeight,
                progress.CompletedWeight,
                progress.TotalTasks,
                progress.CompletedTasks,
                progress.TotalSubtasks,
                progress.CompletedSubtasks,
                DateTime.UtcNow));
        }

        return Ok(new ProjectProgressExportResponse
        {
            Items = items,
            NextCursor = projectIds.Count > normalizedLimit ? EncodeCursor(offset + normalizedLimit) : null
        });
    }

    private bool IsAuthorized()
    {
        return _internalRequestAuthenticator.Authenticate(Request).IsAuthorized;
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(raw, out var offset) && offset >= 0 ? offset : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
    }

    private static IReadOnlyList<string> ParseBoardStickerIds(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(rawValue);
            return parsed?.Where(value => !string.IsNullOrWhiteSpace(value)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed class ProjectProgressExportResponse
    {
        public IReadOnlyList<ManagementProjectProgressSnapshotEventV1> Items { get; init; } = [];
        public string? NextCursor { get; init; }
    }

    private sealed class AnalyticsSnapshotResponse
    {
        public Guid ProjectId { get; init; }
        public Guid WorkspaceId { get; init; }
        public bool HasAccess { get; init; }
        public IReadOnlyList<AnalyticsSnapshotStatusDistributionDto> StatusDistribution { get; init; } = [];
        public IReadOnlyList<AnalyticsSnapshotMemberDto> Members { get; init; } = [];
    }

    private sealed class AnalyticsSnapshotStatusDistributionDto
    {
        public Guid StatusId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Color { get; init; }
        public int Order { get; init; }
        public bool IsFinal { get; init; }
        public int TaskCount { get; set; }
    }

    private sealed class AnalyticsSnapshotMemberDto
    {
        public Guid UserId { get; init; }
        public string Role { get; init; } = string.Empty;
    }

    private sealed class AssistantProjectSnapshotResponse
    {
        public AssistantSnapshotWorkspaceDto Workspace { get; init; } = new();
        public AssistantSnapshotProjectDto Project { get; init; } = new();
        public IReadOnlyList<AssistantSnapshotMemberDto> Members { get; init; } = [];
        public IReadOnlyList<AssistantSnapshotStatusDto> Statuses { get; init; } = [];
        public IReadOnlyList<AssistantSnapshotLabelDto> Labels { get; init; } = [];
        public IReadOnlyList<AssistantSnapshotTaskDto> Tasks { get; init; } = [];
    }

    private sealed class AssistantSnapshotWorkspaceDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid OwnerId { get; init; }
    }

    private sealed class AssistantSnapshotProjectDto
    {
        public Guid Id { get; init; }
        public Guid WorkspaceId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid CreatedBy { get; init; }
        public IReadOnlyList<string> BoardStickerIds { get; init; } = [];
    }

    private sealed class AssistantSnapshotMemberDto
    {
        public Guid UserId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime JoinedAt { get; init; }
    }

    private sealed class AssistantSnapshotStatusDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int Order { get; init; }
        public string? Color { get; init; }
        public bool IsFinal { get; init; }
    }

    private sealed class AssistantSnapshotLabelDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Color { get; init; }
    }

    private sealed class AssistantSnapshotTaskDto
    {
        public Guid Id { get; init; }
        public Guid? StatusId { get; init; }
        public string? StatusName { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid? AssigneeId { get; init; }
        public Guid CreatedBy { get; init; }
        public string Priority { get; init; } = string.Empty;
        public DateTime? DueDate { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public IReadOnlyList<AssistantSnapshotLabelDto> Labels { get; init; } = [];
        public IReadOnlyList<AssistantSnapshotSubTaskDto> SubTasks { get; init; } = [];
        public IReadOnlyList<AssistantSnapshotCommentDto> Comments { get; init; } = [];
    }

    private sealed class AssistantSnapshotSubTaskDto
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool IsCompleted { get; init; }
        public int Order { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    private sealed class AssistantSnapshotCommentDto
    {
        public Guid Id { get; init; }
        public Guid AuthorId { get; init; }
        public string Text { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
