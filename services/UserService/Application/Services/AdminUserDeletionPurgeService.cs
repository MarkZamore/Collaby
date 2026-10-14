using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.Correlation;
using UserService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UserService.Application.Services;

public sealed class AdminUserDeletionPurgeService
{
    private const string ServiceName = "user-service";

    private readonly UserDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public AdminUserDeletionPurgeService(UserDbContext dbContext, IEventPublisher eventPublisher)
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
            var deletedRecords = 0;
            deletedRecords += await _dbContext.UserProfiles.Where(entity => entity.UserId == message.Payload.UserId).ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.UserPreferences.Where(entity => entity.UserId == message.Payload.UserId).ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.UserOnboarding.Where(entity => entity.UserId == message.Payload.UserId).ExecuteDeleteAsync(cancellationToken);
            deletedRecords += await _dbContext.UserActivity.Where(entity => entity.UserId == message.Payload.UserId).ExecuteDeleteAsync(cancellationToken);

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
