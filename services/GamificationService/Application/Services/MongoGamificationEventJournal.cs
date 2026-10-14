using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using GamificationService.Application.Interfaces;
using GamificationService.Contracts.Responses;
using GamificationService.Domain.Enums;
using GamificationService.Infrastructure.Documents;
using MongoDB.Driver;

namespace GamificationService.Application.Services;

public sealed class MongoGamificationEventJournal : IGamificationEventJournal
{
    private readonly IMongoCollection<GamificationEventDocument> _events;
    private readonly ILogger<MongoGamificationEventJournal> _logger;

    public MongoGamificationEventJournal(
        IMongoDatabase database,
        ILogger<MongoGamificationEventJournal> logger)
    {
        _events = database.GetCollection<GamificationEventDocument>("events");
        _logger = logger;
    }

    public async Task AppendAsync(
        ConsumedEvent<ManagementTaskEventV1> request,
        XpReason? reason,
        RewardEventResponse? reward,
        CancellationToken cancellationToken = default)
    {
        var document = new GamificationEventDocument
        {
            SourceEventId = request.Metadata.EventId,
            SourceService = request.Metadata.SourceService,
            EventType = request.Payload.EventName,
            Reason = reason?.ToString(),
            UserId = request.Payload.ActorUserId,
            ProjectId = request.Payload.ProjectId,
            TaskId = request.Payload.TaskId,
            Priority = request.Payload.Priority,
            Metadata = request.Metadata.CorrelationId,
            PayloadJson = JsonSerializer.Serialize(request.Payload),
            RewardJson = reward is null ? null : JsonSerializer.Serialize(reward),
            OccurredAtUtc = request.Metadata.OccurredAtUtc,
            RecordedAtUtc = DateTime.UtcNow
        };

        await _events.InsertOneAsync(document, cancellationToken: cancellationToken);
        _logger.LogDebug(
            "Recorded gamification event {EventType} from {SourceService} with source event {EventId}",
            request.Payload.EventName,
            request.Metadata.SourceService,
            request.Metadata.EventId);
    }
}
