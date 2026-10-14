using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Entities;
using GamificationService.Infrastructure.Data;

namespace GamificationService.Application.Services;

public sealed class AnalyticsRewardOutbox : IAnalyticsRewardOutbox
{
    private const string Destination = EventTopicNames.GamificationProgressEventsV1;
    private const string MessageType = EventTopicNames.GamificationProgressEventsV1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GamificationDbContext _dbContext;

    public AnalyticsRewardOutbox(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task EnqueueAsync(
        ConsumedEvent<ManagementTaskEventV1> sourceEvent,
        RewardEventResponse reward,
        CancellationToken cancellationToken = default)
    {
        var eventType = reward.XpAwarded < 0 || reward.CoinsAwarded < 0
            ? "reward-rolled-back"
            : "reward-applied";
        var occurredAtUtc = DateTime.UtcNow;
        var envelope = new PersistedEventEnvelope<GamificationProgressEventV1>(
            Guid.NewGuid(),
            eventType,
            sourceEvent.Metadata.CorrelationId,
            sourceEvent.Metadata.EventId.ToString("N"),
            occurredAtUtc,
            new GamificationProgressEventV1(
                sourceEvent.Payload.ActorUserId,
                (int)reward.XpAwarded,
                (int)reward.CoinsAwarded,
                reward.NewLevel,
                reward.Reason));

        _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = MessageType,
            Destination = Destination,
            PayloadJson = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAtUtc = occurredAtUtc,
            NextAttemptAtUtc = occurredAtUtc
        });

        return Task.CompletedTask;
    }
}
