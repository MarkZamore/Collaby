using System.Text.Json;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.InternalContracts.Events;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public sealed class OutboxGamificationHook : IGamificationHook
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ManagementDbContext _dbContext;

    public OutboxGamificationHook(ManagementDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task OnTaskCompleted(Guid workspaceId, Guid taskId, Guid userId, Guid projectId, TaskPriority priority)
        => EnqueueAsync(workspaceId, "task-completed", userId, projectId, taskId, priority.ToString().ToLowerInvariant());

    public Task OnTaskReopened(Guid workspaceId, Guid taskId, Guid userId, Guid projectId)
        => EnqueueAsync(workspaceId, "task-reopened", userId, projectId, taskId);

    public Task OnSubtaskCompleted(Guid workspaceId, Guid subtaskId, Guid userId, Guid projectId)
        => EnqueueAsync(workspaceId, "subtask-completed", userId, projectId, subtaskId);

    public Task OnCommentAdded(Guid workspaceId, Guid taskId, Guid userId, Guid projectId)
        => EnqueueAsync(workspaceId, "comment-added", userId, projectId, taskId);

    private Task EnqueueAsync(Guid workspaceId, string eventName, Guid userId, Guid projectId, Guid taskId, string? priority = null)
    {
        var correlationId = CorrelationContext.GetOrCreate();
        var envelope = new PersistedEventEnvelope<ManagementTaskEventV1>(
            EventId: Guid.NewGuid(),
            EventType: eventName,
            CorrelationId: correlationId,
            CausationId: correlationId,
            OccurredAtUtc: DateTime.UtcNow,
            Payload: new ManagementTaskEventV1(
                WorkspaceId: workspaceId,
                ProjectId: projectId,
                TaskId: taskId,
                ActorUserId: userId,
                EventName: eventName,
                Priority: priority));

        _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = EventTopicNames.ManagementTaskEventsV1,
            Destination = EventTopicNames.ManagementTaskEventsV1,
            PayloadJson = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAtUtc = envelope.OccurredAtUtc,
            NextAttemptAtUtc = envelope.OccurredAtUtc
        });

        return Task.CompletedTask;
    }
}
