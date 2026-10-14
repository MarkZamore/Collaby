using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.Correlation;
using MessengerService.Domain.Entities;
using MessengerService.Infrastructure.Data;
using MongoDB.Driver;

namespace MessengerService.Application.Services;

public sealed class AdminUserDeletionPurgeService
{
    private const string ServiceName = "messenger-service";

    private readonly MessengerMongoDatabaseAccessor _databaseAccessor;
    private readonly IEventPublisher _eventPublisher;

    public AdminUserDeletionPurgeService(MessengerMongoDatabaseAccessor databaseAccessor, IEventPublisher eventPublisher)
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
            var database = _databaseAccessor.Database;
            var chats = database.GetCollection<Chat>("chats");
            var messages = database.GetCollection<Message>("messages");
            var userId = message.Payload.UserId;

            var ownedChatIds = await chats
                .Find(chat => chat.CreatedBy == userId)
                .Project(chat => chat.Id)
                .ToListAsync(cancellationToken);

            var affectedRecords = 0L;
            if (ownedChatIds.Count > 0)
            {
                var deletedMessages = await messages.DeleteManyAsync(entity => ownedChatIds.Contains(entity.ChatId), cancellationToken);
                var deletedChats = await chats.DeleteManyAsync(entity => ownedChatIds.Contains(entity.Id), cancellationToken);
                affectedRecords += deletedMessages.DeletedCount + deletedChats.DeletedCount;
            }

            var deletedUserMessages = await messages.DeleteManyAsync(entity => entity.SenderId == userId, cancellationToken);
            affectedRecords += deletedUserMessages.DeletedCount;

            var removedMembers = await chats.UpdateManyAsync(
                Builders<Chat>.Filter.ElemMatch(entity => entity.Members, member => member.UserId == userId),
                Builders<Chat>.Update.PullFilter(entity => entity.Members, member => member.UserId == userId),
                cancellationToken: cancellationToken);
            affectedRecords += removedMembers.ModifiedCount;

            var clearedLastMessage = await chats.UpdateManyAsync(
                Builders<Chat>.Filter.Eq("lastMessage.senderId", userId.ToString()),
                Builders<Chat>.Update.Unset("lastMessage"),
                cancellationToken: cancellationToken);
            affectedRecords += clearedLastMessage.ModifiedCount;

            var deletedEmptyChats = await chats.DeleteManyAsync(
                Builders<Chat>.Filter.Size(entity => entity.Members, 0),
                cancellationToken);
            affectedRecords += deletedEmptyChats.DeletedCount;

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
