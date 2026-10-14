using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.SystemAdministration;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;

namespace IdentityService.Application.Services;

public sealed class IdentityDirectoryOutboxWriter : IIdentityDirectoryOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IdentityDbContext _dbContext;
    private readonly ISystemAdminRoster _systemAdminRoster;

    public IdentityDirectoryOutboxWriter(
        IdentityDbContext dbContext,
        ISystemAdminRoster systemAdminRoster)
    {
        _dbContext = dbContext;
        _systemAdminRoster = systemAdminRoster;
    }

    public void Enqueue(User user, string lifecycleState)
    {
        var correlationId = CorrelationContext.GetOrCreate();
        var envelope = new PersistedEventEnvelope<IdentityUserDirectoryEventV1>(
            EventId: Guid.NewGuid(),
            EventType: lifecycleState,
            CorrelationId: correlationId,
            CausationId: correlationId,
            OccurredAtUtc: DateTime.UtcNow,
            Payload: new IdentityUserDirectoryEventV1(
                user.Id,
                user.Username,
                user.Email,
                user.IsActive,
                _systemAdminRoster.IsSystemAdminEmail(user.Email),
                lifecycleState));

        _dbContext.IntegrationOutboxMessages.Add(new IntegrationOutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = EventTopicNames.IdentityUserDirectoryV1,
            Destination = EventTopicNames.IdentityUserDirectoryV1,
            PayloadJson = JsonSerializer.Serialize(envelope, JsonOptions),
            OccurredAtUtc = envelope.OccurredAtUtc,
            NextAttemptAtUtc = envelope.OccurredAtUtc
        });
    }
}
