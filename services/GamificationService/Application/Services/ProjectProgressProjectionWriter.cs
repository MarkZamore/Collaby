using Collaby.InternalContracts.Events;
using GamificationService.Application.Interfaces;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Application.Services;

public sealed class ProjectProgressProjectionWriter : IProjectProgressProjectionWriter
{
    private readonly GamificationDbContext _dbContext;

    public ProjectProgressProjectionWriter(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ApplySnapshotAsync(
        ManagementProjectProgressSnapshotEventV1 snapshot,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProjectProgressSnapshots.FindAsync([snapshot.ProjectId], cancellationToken);
        if (entity is null)
        {
            entity = new ProjectProgressSnapshotEntry
            {
                ProjectId = snapshot.ProjectId
            };

            _dbContext.ProjectProgressSnapshots.Add(entity);
        }

        Map(entity, snapshot);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplySnapshotsAsync(
        IReadOnlyCollection<ManagementProjectProgressSnapshotEventV1> snapshots,
        CancellationToken cancellationToken = default)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        var projectIds = snapshots.Select(snapshot => snapshot.ProjectId).Distinct().ToArray();
        var existingEntries = await _dbContext.ProjectProgressSnapshots
            .Where(entry => projectIds.Contains(entry.ProjectId))
            .ToDictionaryAsync(entry => entry.ProjectId, cancellationToken);

        foreach (var snapshot in snapshots)
        {
            if (!existingEntries.TryGetValue(snapshot.ProjectId, out var entity))
            {
                entity = new ProjectProgressSnapshotEntry
                {
                    ProjectId = snapshot.ProjectId
                };

                _dbContext.ProjectProgressSnapshots.Add(entity);
                existingEntries[snapshot.ProjectId] = entity;
            }

            Map(entity, snapshot);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ProjectProgressSnapshots.FindAsync([projectId], cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.ProjectProgressSnapshots.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void Map(
        ProjectProgressSnapshotEntry entity,
        ManagementProjectProgressSnapshotEventV1 snapshot)
    {
        entity.ProgressPercent = snapshot.ProgressPercent;
        entity.TotalWeight = snapshot.TotalWeight;
        entity.CompletedWeight = snapshot.CompletedWeight;
        entity.TotalTasks = snapshot.TotalTasks;
        entity.CompletedTasks = snapshot.CompletedTasks;
        entity.TotalSubtasks = snapshot.TotalSubtasks;
        entity.CompletedSubtasks = snapshot.CompletedSubtasks;
        entity.UpdatedAtUtc = snapshot.UpdatedAtUtc;
    }
}
