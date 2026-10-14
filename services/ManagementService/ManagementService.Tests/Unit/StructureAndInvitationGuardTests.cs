using ManagementService.Application.Events;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class StructureAndInvitationGuardTests
{
    [Fact]
    public async Task CreateStatusAsync_WhenStructurePermissionDenied_ShouldThrowAndNotPersistStatus()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = await SeedWorkspaceWithProjectAsync(dbContext, workspaceId, userId);
        var workspaceService = new DenyingWorkspaceService();
        var service = new StatusService(dbContext, new NoOpProjectService(), workspaceService);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateStatusAsync(projectId, "Review", "#8B5CF6", false, userId));

        Assert.Equal(workspaceId, workspaceService.RequestedWorkspaceId);
        Assert.Equal(userId, workspaceService.RequestedUserId);
        Assert.Empty(await dbContext.ProjectStatuses.ToListAsync());
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenEmailAlreadyBelongsToActiveMember_ShouldRejectDuplicateInvite()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        const string memberEmail = "member@example.invalid";

        await SeedWorkspaceAsync(dbContext, workspaceId, ownerId, existingMemberId);

        var emailQueue = new RecordingEmailQueueService();
        var invitationService = new InvitationService(
            dbContext,
            new OwnerWorkspaceService(),
            new ExactEmailUserLookupClient(existingMemberId, memberEmail),
            emailQueue,
            NullLogger<InvitationService>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invitationService.CreateInvitationAsync(workspaceId, memberEmail.ToUpperInvariant(), "Member", 7, ownerId));

        Assert.Contains("already an active member", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await dbContext.WorkspaceInvitations.ToListAsync());
        Assert.Equal(0, emailQueue.QueuedCount);
    }

    [Fact]
    public async Task CreateInvitationAsync_WhenStalePendingInviteExistsForActiveMember_ShouldRevokeItAndReject()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        const string memberEmail = "member@example.invalid";

        await SeedWorkspaceAsync(dbContext, workspaceId, ownerId, existingMemberId);

        var staleInvite = new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Email = memberEmail,
            Role = "Member",
            Token = "stale-token",
            InvitedBy = ownerId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-15),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
        };

        dbContext.WorkspaceInvitations.Add(staleInvite);
        await dbContext.SaveChangesAsync();

        var emailQueue = new RecordingEmailQueueService();
        var invitationService = new InvitationService(
            dbContext,
            new OwnerWorkspaceService(),
            new ExactEmailUserLookupClient(existingMemberId, memberEmail),
            emailQueue,
            NullLogger<InvitationService>.Instance);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invitationService.CreateInvitationAsync(workspaceId, memberEmail, "Member", 7, ownerId));

        Assert.Contains("already an active member", error.Message, StringComparison.OrdinalIgnoreCase);

        var invitation = await dbContext.WorkspaceInvitations.SingleAsync();
        Assert.Equal("Revoked", invitation.Status);
        Assert.Equal(0, emailQueue.QueuedCount);
    }

    [Fact]
    public async Task IsInvitationAlreadySatisfiedByUserAsync_WhenViewerMatchesActiveMemberEmail_ShouldReturnTrue()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var existingMemberId = Guid.NewGuid();
        const string memberEmail = "member@example.invalid";

        await SeedWorkspaceAsync(dbContext, workspaceId, ownerId, existingMemberId);

        var invitationService = new InvitationService(
            dbContext,
            new OwnerWorkspaceService(),
            new ExactEmailUserLookupClient(existingMemberId, memberEmail),
            new RecordingEmailQueueService(),
            NullLogger<InvitationService>.Instance);

        var result = await invitationService.IsInvitationAlreadySatisfiedByUserAsync(
            workspaceId,
            memberEmail.ToUpperInvariant(),
            existingMemberId);

        Assert.True(result);
    }

    private static async Task<Guid> SeedWorkspaceWithProjectAsync(ManagementDbContext dbContext, Guid workspaceId, Guid userId)
    {
        dbContext.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "QA Workspace",
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "QA Project",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        return project.Id;
    }

    private static async Task SeedWorkspaceAsync(
        ManagementDbContext dbContext,
        Guid workspaceId,
        Guid ownerId,
        Guid existingMemberId)
    {
        dbContext.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "QA Workspace",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        dbContext.WorkspaceMembers.AddRange(
            new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = ownerId,
                Role = "Owner",
                Status = "Active",
                JoinedAt = DateTime.UtcNow,
            },
            new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = existingMemberId,
                Role = "Member",
                Status = "Active",
                JoinedAt = DateTime.UtcNow,
                InvitedBy = ownerId,
            });

        await dbContext.SaveChangesAsync();
    }

    private static ManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ManagementDbContext(options);
    }

    private sealed class DenyingWorkspaceService : IWorkspaceService
    {
        public Guid? RequestedWorkspaceId { get; private set; }
        public Guid? RequestedUserId { get; private set; }

        public Task<WorkspaceResponse> CreateWorkspaceAsync(string name, string? description, Guid userId) => throw new NotImplementedException();
        public Task<IEnumerable<WorkspaceResponse>> GetUserWorkspacesAsync(Guid userId) => throw new NotImplementedException();
        public Task<WorkspaceResponse?> GetWorkspaceByIdAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> HasAccessToWorkspaceAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<IEnumerable<WorkspaceMember>> GetWorkspaceMembersAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<List<WorkspaceMemberResponse>> GetWorkspaceMembersEnrichedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AddWorkspaceMemberResult> AddMemberAsync(Guid workspaceId, Guid userIdToAdd, string role, Guid currentUserId) => throw new NotImplementedException();
        public Task<WorkspaceMember?> UpdateMemberRoleAsync(Guid workspaceId, Guid userIdToUpdate, string newRole, Guid currentUserId) => throw new NotImplementedException();
        public Task<bool> RemoveMemberAsync(Guid workspaceId, Guid userIdToRemove, Guid currentUserId) => throw new NotImplementedException();
        public Task<string?> GetUserRoleAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> IsOwnerOrAdminAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task RequireTaskMutationPermissionAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task RequireMemberManagementPermissionAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task RequireWorkspaceOwnershipAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> DeleteWorkspaceAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> AcceptInviteAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> DeclineInviteAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();

        public Task RequireWorkspaceStructurePermissionAsync(Guid workspaceId, Guid userId)
        {
            RequestedWorkspaceId = workspaceId;
            RequestedUserId = userId;
            throw new UnauthorizedAccessException("Structure management is forbidden");
        }
    }

    private sealed class OwnerWorkspaceService : IWorkspaceService
    {
        public Task<WorkspaceResponse> CreateWorkspaceAsync(string name, string? description, Guid userId) => throw new NotImplementedException();
        public Task<IEnumerable<WorkspaceResponse>> GetUserWorkspacesAsync(Guid userId) => throw new NotImplementedException();
        public Task<WorkspaceResponse?> GetWorkspaceByIdAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> HasAccessToWorkspaceAsync(Guid workspaceId, Guid userId) => Task.FromResult(true);
        public Task<IEnumerable<WorkspaceMember>> GetWorkspaceMembersAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<List<WorkspaceMemberResponse>> GetWorkspaceMembersEnrichedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AddWorkspaceMemberResult> AddMemberAsync(Guid workspaceId, Guid userIdToAdd, string role, Guid currentUserId) => throw new NotImplementedException();
        public Task<WorkspaceMember?> UpdateMemberRoleAsync(Guid workspaceId, Guid userIdToUpdate, string newRole, Guid currentUserId) => throw new NotImplementedException();
        public Task<bool> RemoveMemberAsync(Guid workspaceId, Guid userIdToRemove, Guid currentUserId) => throw new NotImplementedException();
        public Task<string?> GetUserRoleAsync(Guid workspaceId, Guid userId) => Task.FromResult<string?>("Owner");
        public Task<bool> IsOwnerOrAdminAsync(Guid workspaceId, Guid userId) => Task.FromResult(true);
        public Task RequireWorkspaceStructurePermissionAsync(Guid workspaceId, Guid userId) => Task.CompletedTask;
        public Task RequireTaskMutationPermissionAsync(Guid workspaceId, Guid userId) => Task.CompletedTask;
        public Task RequireMemberManagementPermissionAsync(Guid workspaceId, Guid userId) => Task.CompletedTask;
        public Task RequireWorkspaceOwnershipAsync(Guid workspaceId, Guid userId) => Task.CompletedTask;
        public Task<bool> DeleteWorkspaceAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> AcceptInviteAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<bool> DeclineInviteAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
    }

    private sealed class NoOpProjectService : IProjectService
    {
        public Task<Project> CreateProjectAsync(Guid workspaceId, string name, string? description, Guid userId) => throw new NotImplementedException();
        public Task EnsureDefaultLabelsAsync(Guid projectId) => throw new NotImplementedException();
        public Task<IEnumerable<Project>> GetProjectsByWorkspaceAsync(Guid workspaceId, Guid userId) => throw new NotImplementedException();
        public Task<Project?> GetProjectByIdAsync(Guid projectId, Guid userId) => throw new NotImplementedException();
        public Task<bool> HasAccessToProjectAsync(Guid projectId, Guid userId) => throw new NotImplementedException();
        public Task<Project?> UpdateBoardStickerIdsAsync(Guid projectId, List<string> stickerIds, Guid userId) => throw new NotImplementedException();
        public Task<bool> DeleteProjectAsync(Guid projectId, Guid userId) => throw new NotImplementedException();
    }

    private sealed class ExactEmailUserLookupClient : IUserLookupClient
    {
        private readonly Guid _userId;
        private readonly string _email;

        public ExactEmailUserLookupClient(Guid userId, string email)
        {
            _userId = userId;
            _email = email;
        }

        public Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<Guid, UserLookupResult> result = userIds.Contains(_userId)
                ? new Dictionary<Guid, UserLookupResult>
                {
                    [_userId] = new UserLookupResult
                    {
                        UserId = _userId,
                        DisplayName = "Member",
                        Username = "member",
                        Email = _email,
                    }
                }
                : new Dictionary<Guid, UserLookupResult>();

            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<UserLookupResult>> SearchUsersAsync(
            string query,
            int limit = 20,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UserLookupResult> result =
            [
                new UserLookupResult
                {
                    UserId = _userId,
                    DisplayName = "Member",
                    Username = "member",
                    Email = _email,
                }
            ];

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingEmailQueueService : IEmailQueueService
    {
        public int QueuedCount { get; private set; }

        public Task QueueInvitationEmailAsync(InvitationEmailEvent emailEvent)
        {
            QueuedCount++;
            return Task.CompletedTask;
        }
    }
}
