using AnalyticsService.Application.Contracts;
using AnalyticsService.Application.Interfaces;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WorkspaceHost;

internal sealed class InProcessManagementProjectAnalyticsSnapshotClient : IManagementProjectAnalyticsSnapshotClient
{
    private readonly ManagementDbContext _dbContext;

    public InProcessManagementProjectAnalyticsSnapshotClient(ManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ManagementProjectAnalyticsSnapshot?> GetProjectAnalyticsSnapshotAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Where(entry => entry.Id == projectId)
            .Select(entry => new { entry.Id, entry.WorkspaceId })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return null;
        }

        var workspace = await _dbContext.Workspaces
            .AsNoTracking()
            .Where(entry => entry.Id == project.WorkspaceId)
            .Select(entry => new { entry.Id, entry.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return null;
        }

        var activeMembers = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .Where(member => member.WorkspaceId == workspace.Id && member.Status == "Active")
            .OrderBy(member => member.JoinedAt)
            .Select(member => new ManagementProjectMemberSnapshotDto
            {
                UserId = member.UserId,
                Role = member.Role
            })
            .ToListAsync(cancellationToken);

        if (activeMembers.All(member => member.UserId != workspace.OwnerId))
        {
            activeMembers.Insert(0, new ManagementProjectMemberSnapshotDto
            {
                UserId = workspace.OwnerId,
                Role = "Owner"
            });
        }

        var hasAccess = workspace.OwnerId == userId || activeMembers.Any(member => member.UserId == userId);

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
            .Select(status => new ProjectStatusDistributionDto
            {
                StatusId = status.Id,
                Name = status.Name,
                Color = status.Color,
                Order = status.Order,
                IsFinal = status.IsFinal,
                TaskCount = taskCountsByStatus.GetValueOrDefault(status.Id)
            })
            .ToListAsync(cancellationToken);

        return new ManagementProjectAnalyticsSnapshot
        {
            ProjectId = project.Id,
            WorkspaceId = workspace.Id,
            HasAccess = hasAccess,
            StatusDistribution = statusDistribution,
            Members = activeMembers
        };
    }
}
