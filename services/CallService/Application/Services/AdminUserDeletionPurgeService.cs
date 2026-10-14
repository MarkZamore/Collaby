using CallService.Domain.Entities;
using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.Correlation;
using CallService.Infrastructure.Data;
using MongoDB.Driver;

namespace CallService.Application.Services;

public sealed class AdminUserDeletionPurgeService
{
    private const string ServiceName = "call-service";

    private readonly CallMongoDatabaseAccessor _databaseAccessor;
    private readonly IEventPublisher _eventPublisher;

    public AdminUserDeletionPurgeService(CallMongoDatabaseAccessor databaseAccessor, IEventPublisher eventPublisher)
    {
        _databaseAccessor = databaseAccessor;
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
            var calls = _databaseAccessor.Database.GetCollection<CallSession>("callSessions");
            var userId = message.Payload.UserId;
            var affectedRecords = 0L;

            var deletedOwnedCalls = await calls.DeleteManyAsync(entity => entity.InitiatorId == userId, cancellationToken);
            affectedRecords += deletedOwnedCalls.DeletedCount;

            var removedParticipants = await calls.UpdateManyAsync(
                Builders<CallSession>.Filter.ElemMatch(entity => entity.Participants, participant => participant.UserId == userId),
                Builders<CallSession>.Update.PullFilter(entity => entity.Participants, participant => participant.UserId == userId),
                cancellationToken: cancellationToken);
            affectedRecords += removedParticipants.ModifiedCount;

            var deletedEmptyCalls = await calls.DeleteManyAsync(
                Builders<CallSession>.Filter.Size(entity => entity.Participants, 0),
                cancellationToken);
            affectedRecords += deletedEmptyCalls.DeletedCount;

            var status = affectedRecords == 0 ? InternalUserPurgeStatuses.Noop : InternalUserPurgeStatuses.Completed;
            await PublishResultAsync(message, status, (int)Math.Min(int.MaxValue, affectedRecords), null, cancellationToken);
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
