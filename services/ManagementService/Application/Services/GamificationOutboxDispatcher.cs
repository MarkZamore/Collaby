using System.Text.Json;
using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Application.Services;

public sealed class GamificationOutboxDispatcher : BackgroundService
{
    private const int BatchSize = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedMessageTypes =
    [
        EventTopicNames.ManagementTaskEventsV1,
        EventTopicNames.ManagementProjectProgressSnapshotV1
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GamificationOutboxDispatcher> _logger;

    public GamificationOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<GamificationOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await DispatchBatchAsync(stoppingToken);
                var delay = processedCount == 0 ? TimeSpan.FromSeconds(3) : TimeSpan.FromMilliseconds(250);
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gamification outbox dispatcher iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var now = DateTime.UtcNow;

        var messages = await dbContext.IntegrationOutboxMessages
            .Where(message =>
                SupportedMessageTypes.Contains(message.MessageType) &&
                message.ProcessedAtUtc == null &&
                message.NextAttemptAtUtc <= now)
            .OrderBy(message => message.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        foreach (var message in messages)
        {
            message.LastAttemptAtUtc = now;
            message.AttemptCount += 1;

            try
            {
                switch (message.MessageType)
                {
                    case EventTopicNames.ManagementTaskEventsV1:
                        await PublishTaskEventAsync(message, publisher, cancellationToken);
                        break;
                    case EventTopicNames.ManagementProjectProgressSnapshotV1:
                        await PublishProjectProgressAsync(message, publisher, cancellationToken);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported outbox message type {message.MessageType}.");
                }

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8))));
                message.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                _logger.LogWarning(
                    ex,
                    "Gamification outbox delivery failed. MessageId={MessageId} Attempt={AttemptCount}",
                    message.Id,
                    message.AttemptCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }

    private static async Task PublishTaskEventAsync(
        Domain.Entities.IntegrationOutboxMessage message,
        IEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<PersistedEventEnvelope<ManagementTaskEventV1>>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize outbox message {message.Id}.");

        await publisher.PublishAsync(
            new EventPublishRequest<ManagementTaskEventV1>(
                EventTopicNames.ManagementTaskEventsV1,
                envelope.Payload.TaskId.ToString("N"),
                envelope.EventType,
                envelope.Payload)
            {
                EventId = envelope.EventId,
                CorrelationId = envelope.CorrelationId,
                CausationId = envelope.CausationId,
                OccurredAtUtc = envelope.OccurredAtUtc
            },
            cancellationToken);
    }

    private static async Task PublishProjectProgressAsync(
        Domain.Entities.IntegrationOutboxMessage message,
        IEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        var envelope = JsonSerializer.Deserialize<PersistedEventEnvelope<ManagementProjectProgressSnapshotEventV1>>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize outbox message {message.Id}.");

        await publisher.PublishAsync(
            new EventPublishRequest<ManagementProjectProgressSnapshotEventV1>(
                EventTopicNames.ManagementProjectProgressSnapshotV1,
                envelope.Payload.ProjectId.ToString("N"),
                envelope.EventType,
                envelope.Payload)
            {
                EventId = envelope.EventId,
                CorrelationId = envelope.CorrelationId,
                CausationId = envelope.CausationId,
                OccurredAtUtc = envelope.OccurredAtUtc
            },
            cancellationToken);
    }
}
