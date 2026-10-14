using AnalyticsService.Infrastructure.Data;
using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.Correlation;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Application.Services;

public sealed class AdminUserDeletionPurgeService
{
    private const string ServiceName = "analytics-service";

    private readonly AnalyticsDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public AdminUserDeletionPurgeService(AnalyticsDbContext dbContext, IEventPublisher eventPublisher)
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
            var userId = message.Payload.UserId;
            var affectedRecords = 0;
            affectedRecords += await _dbContext.UserAnalyticsSummaries.Where(entity => entity.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            affectedRecords += await _dbContext.AnalyticsInboxEvents.Where(entity => entity.UserId == userId).ExecuteDeleteAsync(cancellationToken);

            var status = affectedRecords == 0 ? InternalUserPurgeStatuses.Noop : InternalUserPurgeStatuses.Completed;
            await PublishResultAsync(message, status, affectedRecords, null, cancellationToken);
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
