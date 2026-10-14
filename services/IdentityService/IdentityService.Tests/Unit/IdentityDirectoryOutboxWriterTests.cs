using System.Text.Json;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.SystemAdministration;
using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class IdentityDirectoryOutboxWriterTests
{
    [Fact]
    public void Enqueue_ShouldPersistIdentityUserDirectoryEnvelope()
    {
        using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "founder@collaby.test",
            Username = "founder",
            PasswordHash = "hash",
            IsActive = true
        };

        var writer = new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster());

        writer.Enqueue(user, "activated");
        dbContext.SaveChanges();

        var message = Assert.Single(dbContext.IntegrationOutboxMessages);
        Assert.Equal(EventTopicNames.IdentityUserDirectoryV1, message.MessageType);
        Assert.Equal(EventTopicNames.IdentityUserDirectoryV1, message.Destination);

        var envelope = JsonSerializer.Deserialize<PersistedEventEnvelope<IdentityUserDirectoryEventV1>>(
            message.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(envelope);
        Assert.Equal("activated", envelope!.EventType);
        Assert.Equal(user.Id, envelope.Payload.UserId);
        Assert.Equal(user.Username, envelope.Payload.Username);
        Assert.Equal(user.Email, envelope.Payload.Email);
        Assert.True(envelope.Payload.IsSystemAdmin);
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new IdentityDbContext(options);
    }

    private sealed class StubSystemAdminRoster : ISystemAdminRoster
    {
        public IReadOnlyCollection<string> GetEmails() => ["founder@collaby.test"];

        public bool IsSystemAdminEmail(string? email) =>
            string.Equals(email, "founder@collaby.test", StringComparison.OrdinalIgnoreCase);

        public bool IsSystemAdmin(System.Security.Claims.ClaimsPrincipal principal) =>
            IsSystemAdminEmail(principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);
    }
}
