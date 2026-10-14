using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses.Admin;
using ManagementService.Infrastructure.Data;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Controllers;

[ApiController]
[Authorize(Policy = SystemAdminConstants.PolicyName)]
[Route("admin/workspaces")]
public sealed class AdminWorkspacesController : ControllerBase
{
    private readonly ManagementDbContext _dbContext;
    private readonly IUserLookupClient _userLookupClient;

    public AdminWorkspacesController(
        ManagementDbContext dbContext,
        IUserLookupClient userLookupClient)
    {
        _dbContext = dbContext;
        _userLookupClient = userLookupClient;
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminWorkspaceListResponse>> GetWorkspaces(
        [FromQuery] string? q,
        [FromQuery] bool? isArchived,
        [FromQuery] Guid? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedQuery = q?.Trim();

        var query = _dbContext.Workspaces.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(workspace =>
                EF.Functions.ILike(workspace.Name, $"%{normalizedQuery}%") ||
                (workspace.Description != null && EF.Functions.ILike(workspace.Description, $"%{normalizedQuery}%")));
        }

        if (isArchived.HasValue)
        {
            query = query.Where(workspace => workspace.IsArchived == isArchived.Value);
        }

        if (ownerId.HasValue)
        {
            query = query.Where(workspace => workspace.OwnerId == ownerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var workspaces = await query
            .OrderByDescending(workspace => workspace.CreatedAt)
            .Select(workspace => new WorkspaceProjection
            {
                Id = workspace.Id,
                Name = workspace.Name,
                Description = workspace.Description,
                IsArchived = workspace.IsArchived,
                CreatedAt = workspace.CreatedAt,
                UpdatedAt = workspace.UpdatedAt,
                OwnerId = workspace.OwnerId,
                MemberCount = workspace.Members.Count(),
                ProjectCount = workspace.Projects.Count(),
                TaskCount = workspace.Projects.SelectMany(project => project.Tasks).Count()
            })
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var ownerMap = await BuildUserMapAsync(workspaces.Select(workspace => workspace.OwnerId), cancellationToken);

        return Ok(new AdminWorkspaceListResponse
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            Items = workspaces.Select(workspace => new AdminWorkspaceListItemResponse
            {
                Id = workspace.Id,
                Name = workspace.Name,
                Description = workspace.Description,
                IsArchived = workspace.IsArchived,
                CreatedAt = workspace.CreatedAt,
                UpdatedAt = workspace.UpdatedAt,
                MemberCount = workspace.MemberCount,
                ProjectCount = workspace.ProjectCount,
                TaskCount = workspace.TaskCount,
                Owner = ownerMap.GetValueOrDefault(workspace.OwnerId)
            }).ToArray()
        });
    }

    [HttpGet("{workspaceId:guid}")]
    public async Task<ActionResult<AdminWorkspaceDetailResponse>> GetWorkspace(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Include(entity => entity.Members)
            .FirstOrDefaultAsync(entity => entity.Id == workspaceId, cancellationToken);

        if (workspace is null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        var projectCount = await _dbContext.Projects.CountAsync(project => project.WorkspaceId == workspace.Id, cancellationToken);
        var taskCount = await _dbContext.Tasks.CountAsync(task => task.Project!.WorkspaceId == workspace.Id, cancellationToken);
        var userIds = workspace.Members.Select(member => member.UserId).Append(workspace.OwnerId);
        var userMap = await BuildUserMapAsync(userIds, cancellationToken);

        return Ok(new AdminWorkspaceDetailResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            IsArchived = workspace.IsArchived,
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt,
            MemberCount = workspace.Members.Count,
            ActiveMemberCount = workspace.Members.Count(member => string.Equals(member.Status, "Active", StringComparison.OrdinalIgnoreCase)),
            ProjectCount = projectCount,
            TaskCount = taskCount,
            Owner = userMap.GetValueOrDefault(workspace.OwnerId),
            Members = workspace.Members
                .OrderBy(member => member.JoinedAt)
                .Select(member => new AdminWorkspaceMemberSummaryResponse
                {
                    UserId = member.UserId,
                    Role = member.Role,
                    Status = member.Status,
                    JoinedAt = member.JoinedAt,
                    User = userMap.GetValueOrDefault(member.UserId)
                })
                .ToArray()
        });
    }

    [HttpPost("{workspaceId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(entity => entity.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        workspace.IsArchived = true;
        workspace.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{workspaceId:guid}/unarchive")]
    public async Task<IActionResult> Unarchive(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(entity => entity.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        workspace.IsArchived = false;
        workspace.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{workspaceId:guid}/force")]
    public async Task<IActionResult> ForceDelete(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _dbContext.Workspaces.FirstOrDefaultAsync(entity => entity.Id == workspaceId, cancellationToken);
        if (workspace is null)
        {
            return NotFound(new { code = "workspace_not_found", message = "Workspace not found." });
        }

        _dbContext.Workspaces.Remove(workspace);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<Dictionary<Guid, AdminUserSummaryResponse>> BuildUserMapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        var users = await _userLookupClient.GetUsersByIdsAsync(ids, cancellationToken);
        return users.ToDictionary(
            entry => entry.Key,
            entry => new AdminUserSummaryResponse
            {
                UserId = entry.Value.UserId,
                Username = entry.Value.Username,
                DisplayName = entry.Value.DisplayName,
                AvatarUrl = entry.Value.AvatarUrl
            });
    }

    private sealed class WorkspaceProjection
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsArchived { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public Guid OwnerId { get; init; }
        public int MemberCount { get; init; }
        public int ProjectCount { get; init; }
        public int TaskCount { get; init; }
    }
}
