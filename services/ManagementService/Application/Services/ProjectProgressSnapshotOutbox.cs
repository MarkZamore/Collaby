using System.Text.Json;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.InternalContracts.Events;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public sealed class ProjectProgressSnapshotOutbox : IProjectProgressSnapshotOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ManagementDbContext _dbContext;
    private readonly IProjectProgressSnapshotBuilder _builder;

    public ProjectProgressSnapshotOutbox(
        ManagementDbContext dbContext,
        IProjectProgressSnapshotBuilder builder)
    {
        _dbContext = dbContext;
        _builder = builder;
    }

    public async Task EnqueueSnapshotAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var progress = await _builder.BuildAsync(projectId, cancellationToken);
        if (progress is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var correlationId = CorrelationContext.GetOrCreate();
        var payload = new ManagementProjectProgressSnapshotEventV1(
            progress.ProjectId,
            progress.ProgressPercent,
            progress.TotalWeight,
            progress.CompletedWeight,
            progress.TotalTasks,
            progress.CompletedTasks,
            progress.TotalSubtasks,
            progress.CompletedSubtasks,
            now);

        var envelope = new PersistedEventEnvelope<ManagementProjectProgressSnapshotEventV1>(
            EventId: Guid.NewGuid(),
            EventType: "project-progress-updated",
            CorrelationId: correlationId,
            CausationId: correlationId,
            OccurredAtUtc: now,
            Payload: payload);

        _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = EventTopicNames.ManagementProjectProgressSnapshotV1,
            Destination = EventTopicNames.ManagementProjectProgressSnapshotV1,
            PayloadJson = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAtUtc = envelope.OccurredAtUtc,
            NextAttemptAtUtc = envelope.OccurredAtUtc
        });
    }

    public Task EnqueueProjectRemovedAsync(Guid projectId)
    {
        var now = DateTime.UtcNow;
        var correlationId = CorrelationContext.GetOrCreate();
        var envelope = new PersistedEventEnvelope<ManagementProjectProgressSnapshotEventV1>(
            EventId: Guid.NewGuid(),
            EventType: "project-progress-removed",
            CorrelationId: correlationId,
            CausationId: correlationId,
            OccurredAtUtc: now,
            Payload: new ManagementProjectProgressSnapshotEventV1(
                projectId,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                now));

        _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = EventTopicNames.ManagementProjectProgressSnapshotV1,
            Destination = EventTopicNames.ManagementProjectProgressSnapshotV1,
            PayloadJson = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAtUtc = envelope.OccurredAtUtc,
            NextAttemptAtUtc = envelope.OccurredAtUtc
        });

        return Task.CompletedTask;
    }
}
