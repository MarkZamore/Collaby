using System.Net;
using System.Net.Http.Json;
using Collaby.ServiceDefaults.InternalAuth;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ManagementService.Tests.Integration;

public sealed class InternalProjectsControllerTests : IClassFixture<ManagementServiceTestServer>
{
    private readonly HttpClient _client;
    private readonly IServiceProvider _services;

    public InternalProjectsControllerTests(ManagementServiceTestServer server)
    {
        _client = server.Client;
        _services = server.Services;
    }

    [Fact]
    public async Task GetAnalyticsSnapshot_ShouldReturnDistributionMembersAndAccess()
    {
        var ownerId = Guid.NewGuid();
        var activeMemberId = Guid.NewGuid();
        var inactiveMemberId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var todoStatusId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();

        await SeedProjectAsync(
            ownerId,
            activeMemberId,
            inactiveMemberId,
            workspaceId,
            projectId,
            todoStatusId,
            doneStatusId);

        using var request = CreateInternalRequest(
            $"/api/management/internal/v1/queries/projects/{projectId}/analytics-snapshot?userId={activeMemberId}");

        using var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsSnapshotResponse>();

        Assert.NotNull(payload);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal(workspaceId, payload.WorkspaceId);
        Assert.True(payload.HasAccess);
        Assert.Equal(2, payload.StatusDistribution.Count);
        Assert.Contains(payload.StatusDistribution, status => status.Name == "Todo" && status.TaskCount == 2);
        Assert.Contains(payload.StatusDistribution, status => status.Name == "Done" && status.TaskCount == 1 && status.IsFinal);
        Assert.Contains(payload.Members, member => member.UserId == ownerId && member.Role == "Owner");
        Assert.Contains(payload.Members, member => member.UserId == activeMemberId && member.Role == "Member");
        Assert.DoesNotContain(payload.Members, member => member.UserId == inactiveMemberId);
    }

    [Fact]
    public async Task GetAnalyticsSnapshot_ShouldReturnHasAccessFalse_ForOutsider()
    {
        var ownerId = Guid.NewGuid();
        var activeMemberId = Guid.NewGuid();
        var inactiveMemberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var todoStatusId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();

        await SeedProjectAsync(
            ownerId,
            activeMemberId,
            inactiveMemberId,
            workspaceId,
            projectId,
            todoStatusId,
            doneStatusId);

        using var request = CreateInternalRequest(
            $"/api/management/internal/v1/queries/projects/{projectId}/analytics-snapshot?userId={outsiderId}");

        using var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AnalyticsSnapshotResponse>();

        Assert.NotNull(payload);
        Assert.False(payload.HasAccess);
    }

    private async Task SeedProjectAsync(
        Guid ownerId,
        Guid activeMemberId,
        Guid inactiveMemberId,
        Guid workspaceId,
        Guid projectId,
        Guid todoStatusId,
        Guid doneStatusId)
    {
        await using var scope = _services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();

        dbContext.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Workspace",
            OwnerId = ownerId
        });

        dbContext.WorkspaceMembers.AddRange(
            new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = activeMemberId,
                Role = "Member",
                Status = "Active"
            },
            new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = inactiveMemberId,
                Role = "Guest",
                Status = "Inactive"
            });

        dbContext.Projects.Add(new Project
        {
            Id = projectId,
            WorkspaceId = workspaceId,
            Name = "Analytics Project",
            CreatedBy = ownerId
        });

        dbContext.ProjectStatuses.AddRange(
            new ProjectStatus
            {
                Id = todoStatusId,
                ProjectId = projectId,
                Name = "Todo",
                Order = 1,
                IsFinal = false
            },
            new ProjectStatus
            {
                Id = doneStatusId,
                ProjectId = projectId,
                Name = "Done",
                Order = 2,
                IsFinal = true
            });

        dbContext.Tasks.AddRange(
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StatusId = todoStatusId,
                Title = "Task 1",
                CreatedBy = ownerId
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StatusId = todoStatusId,
                Title = "Task 2",
                CreatedBy = ownerId
            },
            new TaskItem
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                StatusId = doneStatusId,
                Title = "Task 3",
                CreatedBy = ownerId
            });

        await dbContext.SaveChangesAsync();
    }

    private static HttpRequestMessage CreateInternalRequest(string relativeUrl)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.TryAddWithoutValidation(InternalAuthHeaderNames.CallerService, "analytics-service");
        request.Headers.TryAddWithoutValidation(InternalAuthHeaderNames.Credential, "analytics-secret");
        return request;
    }

    private sealed class AnalyticsSnapshotResponse
    {
        public Guid ProjectId { get; set; }
        public Guid WorkspaceId { get; set; }
        public bool HasAccess { get; set; }
        public List<AnalyticsSnapshotStatusResponse> StatusDistribution { get; set; } = [];
        public List<AnalyticsSnapshotMemberResponse> Members { get; set; } = [];
    }

    private sealed class AnalyticsSnapshotStatusResponse
    {
        public Guid StatusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsFinal { get; set; }
        public int TaskCount { get; set; }
    }

    private sealed class AnalyticsSnapshotMemberResponse
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
