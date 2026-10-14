using Microsoft.EntityFrameworkCore;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public sealed class ProjectProgressSnapshotBuilder : IProjectProgressSnapshotBuilder
{
    private readonly ManagementDbContext _dbContext;

    public ProjectProgressSnapshotBuilder(ManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectProgressResponse?> BuildAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var projectExists = await _dbContext.Projects
            .AsNoTracking()
            .AnyAsync(project => project.Id == projectId, cancellationToken);

        if (!projectExists)
        {
            return null;
        }

        var taskRows = await _dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId && !task.IsArchived)
            .Select(task => new
            {
                IsCompleted = task.Status != null && task.Status.IsFinal,
                Completion = task.Status != null && task.Status.IsFinal
                    ? 1d
                    : task.SubTasks.Count == 0
                        ? 0d
                        : (double)task.SubTasks.Count(subtask => subtask.IsCompleted) / task.SubTasks.Count,
                TotalSubtasks = task.SubTasks.Count,
                CompletedSubtasks = task.SubTasks.Count(subtask => subtask.IsCompleted)
            })
            .ToListAsync(cancellationToken);

        var totalTasks = taskRows.Count;
        var completedTasks = taskRows.Count(task => task.IsCompleted);
        var totalSubtasks = taskRows.Sum(task => task.TotalSubtasks);
        var completedSubtasks = taskRows.Sum(task => task.CompletedSubtasks);
        var totalWeight = taskRows.Count;
        var completedWeight = taskRows.Sum(task => task.Completion);

        return new ProjectProgressResponse
        {
            ProjectId = projectId,
            TotalWeight = totalWeight,
            CompletedWeight = Math.Round(completedWeight, 2),
            TotalTasks = totalTasks,
            CompletedTasks = completedTasks,
            TotalSubtasks = totalSubtasks,
            CompletedSubtasks = completedSubtasks,
            ProgressPercent = totalWeight == 0
                ? 0
                : Math.Round(completedWeight / totalWeight * 100d, 2)
        };
    }
}
