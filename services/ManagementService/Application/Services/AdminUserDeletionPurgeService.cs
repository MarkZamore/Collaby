using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.Correlation;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Application.Services;

public sealed class AdminUserDeletionPurgeService
{
    private const string ServiceName = "management-service";

    private readonly ManagementDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public AdminUserDeletionPurgeService(ManagementDbContext dbContext, IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    public async Task HandleAsync(ConsumedEvent<AdminUserDeletionCommandV1> message, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(message.Payload.TargetService, ServiceName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        using var correlationScope = CorrelationContext.BeginScope(message.Metadata.CorrelationId);

        try
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var deletedRecords = 0;
            var userId = message.Payload.UserId;
            var ownedWorkspaceIds = await _dbContext.Workspaces
                .Where(workspace => workspace.OwnerId == userId)
                .Select(workspace => workspace.Id)
                .ToListAsync(cancellationToken);

            if (ownedWorkspaceIds.Count > 0)
            {
                deletedRecords += await _dbContext.Workspaces
                    .Where(workspace => ownedWorkspaceIds.Contains(workspace.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            deletedRecords += await _dbContext.TaskComments
                .Where(comment => comment.AuthorId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.Tasks
                .Where(task => task.CreatedBy == userId || task.AssigneeId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.Projects
                .Where(project => project.CreatedBy == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.WorkspaceInvitations
                .Where(invitation => invitation.InvitedBy == userId || invitation.AcceptedBy == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.WorkspaceMembers
                .Where(member => member.UserId == userId || member.InvitedBy == userId)
                .ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.Notifications
                .Where(notification => notification.UserId == userId || notification.ActorId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            var status = deletedRecords == 0 ? InternalUserPurgeStatuses.Noop : InternalUserPurgeStatuses.Completed;
            await PublishResultAsync(message, status, deletedRecords, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await PublishResultAsync(message, "failed", null, ex.Message, cancellationToken);
        }
    }

    private Task PublishResultAsync(
        ConsumedEvent<AdminUserDeletionCommandV1> command,
        string status,
        int? deletedRecords,
        string? failureCode,
        CancellationToken cancellationToken)
    {
        var eventType = status switch
        {
            InternalUserPurgeStatuses.Completed => "user-deletion-completed",
            InternalUserPurgeStatuses.Noop => "user-deletion-noop",
            _ => "user-deletion-failed"
        };

        return _eventPublisher.PublishAsync(
            new EventPublishRequest<AdminUserDeletionResultV1>(
                EventTopicNames.AdminUserDeletionResultsV1,
                command.Payload.UserId.ToString("D"),
                eventType,
                new AdminUserDeletionResultV1(
                    command.Payload.OperationId,
                    command.Payload.UserId,
                    ServiceName,
                    status == InternalUserPurgeStatuses.Completed || status == InternalUserPurgeStatuses.Noop ? status : "failed",
                    deletedRecords,
                    failureCode))
            {
                CorrelationId = command.Metadata.CorrelationId,
                CausationId = command.Metadata.EventId.ToString("N"),
                OccurredAtUtc = DateTime.UtcNow
            },
            cancellationToken);
    }
}
