using Collaby.InternalContracts.Events;
using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;

namespace UserService.Application.Services;

public sealed class IdentityDirectoryProjectionWriter : IIdentityDirectoryProjectionWriter
{
    private readonly UserDbContext _dbContext;

    public IdentityDirectoryProjectionWriter(UserDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ApplyAsync(
        IReadOnlyCollection<IdentityUserDirectoryEventV1> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            return;
        }

        var userIds = entries.Select(entry => entry.UserId).Distinct().ToArray();
        var existingEntries = await _dbContext.IdentityDirectoryEntries
            .Where(entry => userIds.Contains(entry.UserId))
            .ToDictionaryAsync(entry => entry.UserId, cancellationToken);

        foreach (var snapshot in entries)
        {
            if (string.Equals(snapshot.LifecycleState, "delete-completed", StringComparison.OrdinalIgnoreCase))
            {
                if (existingEntries.TryGetValue(snapshot.UserId, out var existing))
                {
                    _dbContext.IdentityDirectoryEntries.Remove(existing);
                }

                continue;
            }

            if (!existingEntries.TryGetValue(snapshot.UserId, out var entity))
            {
                entity = new IdentityDirectoryEntry
                {
                    UserId = snapshot.UserId
                };

                _dbContext.IdentityDirectoryEntries.Add(entity);
                existingEntries[snapshot.UserId] = entity;
            }

            entity.Username = snapshot.Username;
            entity.Email = snapshot.Email;
            entity.IsActive = snapshot.IsActive;
            entity.IsSystemAdmin = snapshot.IsSystemAdmin;
            entity.LifecycleState = snapshot.LifecycleState;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
