using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class ServiceResilienceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task BootstrapService_WhenUserLookupFails_ShouldReturnPartialResponse()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedWorkspaceAsync(dbContext);
        var service = new BootstrapService(
            dbContext,
            new ThrowingUserLookupClient(),
            NullLogger<BootstrapService>.Instance);

        var response = await service.GetBootstrapDataAsync(userId);

        Assert.True(response.Partial);
        Assert.NotEmpty(response.Warnings);
        Assert.Single(response.Workspaces);
        Assert.NotNull(response.UserProfile);
        Assert.Equal(userId, response.UserProfile!.UserId);
    }

    [Fact]
    public async Task WorkspaceService_WhenUserLookupFails_ShouldStillReturnWorkspaceMembers()
    {
        await using var dbContext = CreateDbContext();
        var userId = await SeedWorkspaceAsync(dbContext);
        var workspaceId = await dbContext.Workspaces.Select(workspace => workspace.Id).SingleAsync();

        var service = new WorkspaceService(
            dbContext,
            new ThrowingUserLookupClient(),
            NullLogger<WorkspaceService>.Instance);

        var workspace = await service.GetWorkspaceByIdAsync(workspaceId, userId);

        Assert.NotNull(workspace);
        var member = Assert.Single(workspace!.Members);
        Assert.Equal(userId, member.UserId);
        Assert.Null(member.Username);
        Assert.Null(member.DisplayName);
        Assert.Null(member.AvatarUrl);
    }

    [Fact]
    public async Task WorkspaceService_ReinvitePendingMember_ShouldNotRequestSecondInviteNotification()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = await SeedWorkspaceAsync(dbContext);
        var workspaceId = await dbContext.Workspaces.Select(workspace => workspace.Id).SingleAsync();
        var invitedUserId = Guid.NewGuid();

        var service = new WorkspaceService(
            dbContext,
            new ThrowingUserLookupClient(),
            NullLogger<WorkspaceService>.Instance);

        var firstInvite = await service.AddMemberAsync(workspaceId, invitedUserId, "Member", ownerId);
        var secondInvite = await service.AddMemberAsync(workspaceId, invitedUserId, "Admin", ownerId);

        Assert.True(firstInvite.ShouldSendInviteNotification);
        Assert.False(secondInvite.ShouldSendInviteNotification);

        var pendingMember = await dbContext.WorkspaceMembers
            .Where(member => member.WorkspaceId == workspaceId && member.UserId == invitedUserId)
            .ToListAsync();

        var member = Assert.Single(pendingMember);
        Assert.Equal("Pending", member.Status);
        Assert.Equal("Admin", member.Role);
    }

    [Fact]
    public async Task OutboxGamificationHook_ShouldPersistCanonicalManagementTaskEvent()
    {
        await using var dbContext = CreateDbContext();
        var hook = new OutboxGamificationHook(dbContext);

        var workspaceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await hook.OnTaskCompleted(workspaceId, taskId, userId, projectId, TaskPriority.High);
        await dbContext.SaveChangesAsync();

        var message = await dbContext.IntegrationOutboxMessages.SingleAsync();
        var envelope = JsonSerializer.Deserialize<PersistedEventEnvelope<ManagementTaskEventV1>>(message.PayloadJson, JsonOptions);

        Assert.Equal(EventTopicNames.ManagementTaskEventsV1, message.Destination);
        Assert.Equal(EventTopicNames.ManagementTaskEventsV1, message.MessageType);
        Assert.NotNull(envelope);
        Assert.Equal("task-completed", envelope!.EventType);
        Assert.Equal(workspaceId, envelope.Payload.WorkspaceId);
        Assert.Equal(projectId, envelope.Payload.ProjectId);
        Assert.Equal(taskId, envelope.Payload.TaskId);
        Assert.Equal(userId, envelope.Payload.ActorUserId);
        Assert.Equal("high", envelope.Payload.Priority);
    }

    private static async Task<Guid> SeedWorkspaceAsync(ManagementDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        dbContext.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Core workspace",
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        dbContext.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userId,
            Role = "Owner",
            Status = "Active",
            JoinedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static ManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ManagementDbContext(options);
    }

    private sealed class ThrowingUserLookupClient : IUserLookupClient
    {
        public Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("UserService unavailable");

        public Task<IReadOnlyList<UserLookupResult>> SearchUsersAsync(
            string query,
            int limit = 20,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("UserService unavailable");
    }
}

