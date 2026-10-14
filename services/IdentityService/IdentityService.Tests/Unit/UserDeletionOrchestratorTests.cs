using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.SystemAdministration;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Settings;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class UserDeletionOrchestratorTests
{
    [Fact]
    public async Task StartAndProcessPendingOperations_ShouldDeactivateUser_And_PublishKafkaCommands()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext);
        var publisher = new RecordingEventPublisher();
        var catalog = new InMemoryUserDeletionTargetCatalog(
        [
            new UserDeletionTargetDefinition { ServiceName = "user-service", Enabled = true },
            new UserDeletionTargetDefinition { ServiceName = "management-service", Enabled = true }
        ]);

        var orchestrator = new UserDeletionOrchestrator(
            dbContext,
            catalog,
            publisher,
            new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster()),
            NullLogger<UserDeletionOrchestrator>.Instance);

        var operation = await orchestrator.StartDeletionAsync(user.Id, Guid.NewGuid(), "admin@collaby.test");
        await orchestrator.ProcessPendingOperationsAsync();

        var reloadedUser = await dbContext.Users.Include(entity => entity.RefreshTokens).SingleAsync(entity => entity.Id == user.Id);
        var reloadedOperation = await dbContext.UserDeletionOperations.Include(entity => entity.Steps).SingleAsync(entity => entity.Id == operation.Id);

        Assert.False(reloadedUser.IsActive);
        Assert.All(reloadedUser.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(UserDeletionOperationStatuses.Running, reloadedOperation.Status);
        Assert.Equal(2, publisher.Events.Count);
        Assert.All(publisher.Events, published => Assert.Equal(EventTopicNames.AdminUserDeletionCommandsV1, published.Topic));
        Assert.Equal(
            ["management-service", "user-service"],
            publisher.Events.Select(published => ((AdminUserDeletionCommandV1)published.Payload).TargetService).OrderBy(value => value).ToArray());
    }

    [Fact]
    public async Task ApplyDeletionResultAsync_ShouldDeleteAuthRecordOnlyAfterAllRequiredTargetsFinish()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext);
        var publisher = new RecordingEventPublisher();
        var catalog = new InMemoryUserDeletionTargetCatalog(
        [
            new UserDeletionTargetDefinition { ServiceName = "user-service", Enabled = true },
            new UserDeletionTargetDefinition { ServiceName = "management-service", Enabled = true }
        ]);

        var orchestrator = new UserDeletionOrchestrator(
            dbContext,
            catalog,
            publisher,
            new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster()),
            NullLogger<UserDeletionOrchestrator>.Instance);

        var operation = await orchestrator.StartDeletionAsync(user.Id, null, "admin@collaby.test");

        await orchestrator.ApplyDeletionResultAsync(
            CreateMetadata("user-deletion-completed"),
            new AdminUserDeletionResultV1(operation.Id, user.Id, "user-service", "completed", 4, null));

        Assert.NotNull(await dbContext.Users.FindAsync(user.Id));
        Assert.Equal(
            UserDeletionOperationStatuses.Running,
            (await dbContext.UserDeletionOperations.FindAsync(operation.Id))!.Status);

        await orchestrator.ApplyDeletionResultAsync(
            CreateMetadata("user-deletion-noop"),
            new AdminUserDeletionResultV1(operation.Id, user.Id, "management-service", "noop", null, null));

        Assert.Null(await dbContext.Users.FindAsync(user.Id));
        var completedOperation = await dbContext.UserDeletionOperations.Include(entity => entity.Steps).SingleAsync(entity => entity.Id == operation.Id);
        Assert.Equal(UserDeletionOperationStatuses.Succeeded, completedOperation.Status);
        Assert.Contains(completedOperation.Steps, step => step.ServiceName == "user-service" && step.Status == UserDeletionOperationStepStatuses.Succeeded);
        Assert.Contains(completedOperation.Steps, step => step.ServiceName == "management-service" && step.Status == UserDeletionOperationStepStatuses.Noop);
    }

    [Fact]
    public async Task StartDeletionAsync_ShouldResumeStaleRunningOperation_InsteadOfCreatingConflict()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext);
        var staleOperation = new UserDeletionOperation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserEmail = user.Email,
            Status = UserDeletionOperationStatuses.Running,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTime.UtcNow.AddHours(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            StartedAtUtc = DateTime.UtcNow.AddHours(-2),
            Steps =
            [
                new UserDeletionOperationStep
                {
                    ServiceName = "user-service",
                    StepOrder = 0,
                    Status = UserDeletionOperationStepStatuses.Running,
                    AttemptCount = 1,
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-30)
                },
                new UserDeletionOperationStep
                {
                    ServiceName = "management-service",
                    StepOrder = 1,
                    Status = UserDeletionOperationStepStatuses.Running,
                    AttemptCount = 1,
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-30)
                }
            ]
        };
        dbContext.UserDeletionOperations.Add(staleOperation);
        await dbContext.SaveChangesAsync();

        var publisher = new RecordingEventPublisher();
        var catalog = new InMemoryUserDeletionTargetCatalog(
        [
            new UserDeletionTargetDefinition { ServiceName = "user-service", Enabled = true },
            new UserDeletionTargetDefinition { ServiceName = "management-service", Enabled = true }
        ]);

        var orchestrator = new UserDeletionOrchestrator(
            dbContext,
            catalog,
            publisher,
            new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster()),
            NullLogger<UserDeletionOrchestrator>.Instance);

        var resumedOperation = await orchestrator.StartDeletionAsync(user.Id, Guid.NewGuid(), "admin@collaby.test");

        Assert.Equal(staleOperation.Id, resumedOperation.Id);
        Assert.Equal(UserDeletionOperationStatuses.RetryRequested, resumedOperation.Status);
        Assert.Equal(1, resumedOperation.RetryCount);
        Assert.All(
            resumedOperation.Steps,
            step =>
            {
                Assert.Equal(UserDeletionOperationStepStatuses.Pending, step.Status);
                Assert.Equal(1, step.AttemptCount);
                Assert.Null(step.CompletedAtUtc);
                Assert.Null(step.StartedAtUtc);
                Assert.Null(step.LastError);
            });
    }

    [Fact]
    public async Task StartDeletionAsync_ShouldRejectRecentRunningOperation()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext);
        dbContext.UserDeletionOperations.Add(new UserDeletionOperation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserEmail = user.Email,
            Status = UserDeletionOperationStatuses.Running,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddSeconds(-20),
            StartedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            Steps =
            [
                new UserDeletionOperationStep
                {
                    ServiceName = "user-service",
                    StepOrder = 0,
                    Status = UserDeletionOperationStepStatuses.Running,
                    AttemptCount = 1,
                    StartedAtUtc = DateTime.UtcNow.AddSeconds(-20)
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var orchestrator = new UserDeletionOrchestrator(
            dbContext,
            new InMemoryUserDeletionTargetCatalog(
            [
                new UserDeletionTargetDefinition { ServiceName = "user-service", Enabled = true }
            ]),
            new RecordingEventPublisher(),
            new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster()),
            NullLogger<UserDeletionOrchestrator>.Instance);

        var exception = await Assert.ThrowsAsync<AdminUserGovernanceException>(() =>
            orchestrator.StartDeletionAsync(user.Id, Guid.NewGuid(), "admin@collaby.test"));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal("hard_delete_already_started", exception.Code);
    }

    [Fact]
    public async Task StartDeletionAsync_ShouldResumeOperation_WhenHeartbeatUpdatesMaskStaleSteps()
    {
        await using var dbContext = CreateDbContext();
        var user = await SeedUserAsync(dbContext);
        var heartbeatMaskedOperation = new UserDeletionOperation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            UserEmail = user.Email,
            Status = UserDeletionOperationStatuses.Running,
            CorrelationId = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTime.UtcNow.AddHours(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddSeconds(-20),
            StartedAtUtc = DateTime.UtcNow.AddHours(-2),
            Steps =
            [
                new UserDeletionOperationStep
                {
                    ServiceName = "user-service",
                    StepOrder = 0,
                    Status = UserDeletionOperationStepStatuses.Running,
                    AttemptCount = 1,
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-20)
                }
            ]
        };
        dbContext.UserDeletionOperations.Add(heartbeatMaskedOperation);
        await dbContext.SaveChangesAsync();

        var orchestrator = new UserDeletionOrchestrator(
            dbContext,
            new InMemoryUserDeletionTargetCatalog(
            [
                new UserDeletionTargetDefinition { ServiceName = "user-service", Enabled = true }
            ]),
            new RecordingEventPublisher(),
            new IdentityDirectoryOutboxWriter(dbContext, new StubSystemAdminRoster()),
            NullLogger<UserDeletionOrchestrator>.Instance);

        var resumedOperation = await orchestrator.StartDeletionAsync(user.Id, Guid.NewGuid(), "admin@collaby.test");

        Assert.Equal(heartbeatMaskedOperation.Id, resumedOperation.Id);
        Assert.Equal(UserDeletionOperationStatuses.RetryRequested, resumedOperation.Status);
        Assert.Equal(1, resumedOperation.RetryCount);
        Assert.All(resumedOperation.Steps, step => Assert.Equal(UserDeletionOperationStepStatuses.Pending, step.Status));
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new IdentityDbContext(options);
    }

    private static async Task<User> SeedUserAsync(IdentityDbContext dbContext)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@collaby.test",
            Username = "user",
            PasswordHash = "hash",
            RefreshTokens =
            [
                new RefreshToken
                {
                    Token = "refresh-token",
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                }
            ]
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static EventMetadata CreateMetadata(string eventType)
    {
        return new EventMetadata(
            Guid.NewGuid(),
            eventType,
            "v1",
            "test-service",
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            DateTime.UtcNow);
    }

    private sealed class InMemoryUserDeletionTargetCatalog : IUserDeletionTargetCatalog
    {
        private readonly IReadOnlyList<UserDeletionTargetDefinition> _targets;

        public InMemoryUserDeletionTargetCatalog(IReadOnlyList<UserDeletionTargetDefinition> targets)
        {
            _targets = targets;
        }

        public IReadOnlyList<UserDeletionTargetDefinition> GetEnabledTargets() =>
            _targets.Where(static target => target.Enabled).ToArray();

        public UserDeletionTargetDefinition? Find(string serviceName) =>
            _targets.FirstOrDefault(target => string.Equals(target.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<PublishedEvent> Events { get; } = [];

        public Task PublishAsync<TPayload>(EventPublishRequest<TPayload> request, CancellationToken cancellationToken = default)
        {
            Events.Add(new PublishedEvent(request.Topic, request.EventType, request.Payload!));
            return Task.CompletedTask;
        }
    }

    private sealed record PublishedEvent(string Topic, string EventType, object Payload);

    private sealed class StubSystemAdminRoster : ISystemAdminRoster
    {
        public IReadOnlyCollection<string> GetEmails() => [];

        public bool IsSystemAdminEmail(string? email) => false;

        public bool IsSystemAdmin(System.Security.Claims.ClaimsPrincipal principal) => false;
    }
}
