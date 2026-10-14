using System.Text.Json;
using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Application.Services;

public sealed class AnalyticsOutboxDispatcher : BackgroundService
{
    private const int BatchSize = 20;
    private const string MessageType = EventTopicNames.GamificationProgressEventsV1;
    private const string AnalyticsDestination = EventTopicNames.GamificationProgressEventsV1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsOutboxDispatcher> _logger;

    public AnalyticsOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        ILogger<AnalyticsOutboxDispatcher> logger)
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
                _logger.LogWarning(ex, "Analytics outbox dispatcher iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        var now = DateTime.UtcNow;

        var messages = await dbContext.IntegrationOutboxMessages
            .Where(message =>
                message.MessageType == MessageType &&
                message.Destination == AnalyticsDestination &&
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
                var envelope = JsonSerializer.Deserialize<PersistedEventEnvelope<GamificationProgressEventV1>>(message.PayloadJson, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to deserialize outbox message {message.Id}.");

                await eventPublisher.PublishAsync(
                    new EventPublishRequest<GamificationProgressEventV1>(
                        EventTopicNames.GamificationProgressEventsV1,
                        envelope.Payload.UserId.ToString("D"),
                        envelope.EventType,
                        envelope.Payload)
                    {
                        EventId = envelope.EventId,
                        CorrelationId = envelope.CorrelationId,
                        CausationId = envelope.CausationId,
                        OccurredAtUtc = envelope.OccurredAtUtc
                    },
                    cancellationToken);
                message.ProcessedAtUtc = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, Math.Min(message.AttemptCount, 8))));
                message.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];

                _logger.LogWarning(
                    ex,
                    "Analytics outbox delivery failed. MessageId={MessageId} Attempt={AttemptCount}",
                    message.Id,
                    message.AttemptCount);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
