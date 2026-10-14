using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AnalyticsService.Application.Contracts;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Application.Services;
using AnalyticsService.Controllers;
using AnalyticsService.Domain.Entities;
using AnalyticsService.Infrastructure.Data;
using Collaby.EventBus;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.Health;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AnalyticsService.Tests.Integration;

public sealed class AnalyticsServiceTestServer : IAsyncLifetime
{
    private WebApplication? _app;
    private readonly string _databaseName = $"analytics-tests-{Guid.NewGuid():N}";

    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _app?.Services ?? throw new InvalidOperationException("Test server is not initialized.");
    public AnalyticsEndpointTests.FakeManagementProjectAnalyticsSnapshotClient SnapshotClient { get; } = new();
    public AnalyticsEndpointTests.FakeUserLookupClient UserLookupClient { get; } = new();

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(AnalyticsSummaryController).Assembly);
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AnalyticsEndpointTests.TestJwtAuthHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ServiceReadinessState>();
        builder.Services.AddDbContext<AnalyticsDbContext>(options =>
        {
            options.UseInMemoryDatabase(_databaseName);
        });
        builder.Services.AddScoped<AnalyticsProjectionService>();
        builder.Services.AddScoped<IProjectAnalyticsOverviewService, ProjectAnalyticsOverviewService>();
        builder.Services.AddSingleton<IManagementProjectAnalyticsSnapshotClient>(SnapshotClient);
        builder.Services.AddSingleton<IUserLookupClient>(UserLookupClient);

        _app = builder.Build();
        var readinessState = _app.Services.GetRequiredService<ServiceReadinessState>();

        _app.UsePathBase("/api/analytics");
        _app.UseRouting();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapGet("/", () => Results.Ok(new { status = "alive", service = "analytics" }));
        _app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "analytics" }));
        _app.MapGet("/health/ready", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
        {
            if (!state.IsReady)
            {
                return Results.Json(
                    new
                    {
                        status = "unhealthy",
                        service = "analytics",
                        reason = state.FailureReason ?? "Startup checks have not completed yet"
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!await dbContext.Database.CanConnectAsync())
            {
                return Results.Json(
                    new { status = "unhealthy", service = "analytics", reason = "Database is unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new { status = "ready", service = "analytics" });
        });
        _app.MapGet("/health", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
        {
            if (!state.IsReady)
            {
                return Results.Json(
                    new
                    {
                        status = "unhealthy",
                        service = "analytics",
                        reason = state.FailureReason ?? "Startup checks have not completed yet"
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!await dbContext.Database.CanConnectAsync())
            {
                return Results.Json(
                    new { status = "unhealthy", service = "analytics", reason = "Database is unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new { status = "ready", service = "analytics" });
        });

        readinessState.MarkReady();
        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

public sealed class AnalyticsEndpointTests : IClassFixture<AnalyticsServiceTestServer>
{
    private readonly HttpClient _client;
    private readonly IServiceProvider _services;
    private readonly AnalyticsServiceTestServer _server;

    public AnalyticsEndpointTests(AnalyticsServiceTestServer server)
    {
        _server = server;
        _client = server.Client;
        _services = server.Services;
        _server.SnapshotClient.Reset();
        _server.UserLookupClient.Reset();
        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact]
    public async Task LiveEndpoint_ShouldReturnAlivePayload()
    {
        var response = await _client.GetAsync("/api/analytics/health/live");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.NotNull(payload);
        Assert.Equal("alive", payload["status"]);
        Assert.Equal("analytics", payload["service"]);
    }

    [Fact]
    public async Task ReadinessEndpoint_ShouldReturnReadyPayload()
    {
        var response = await _client.GetAsync("/api/analytics/health/ready");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.NotNull(payload);
        Assert.Equal("ready", payload["status"]);
        Assert.Equal("analytics", payload["service"]);
    }

    [Fact]
    public async Task ManagementTaskIngestion_ShouldBuildUserAndProjectSummaries()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var scope = _services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();

        var ingestResult = await projectionService.IngestManagementTaskAsync(
            new EventMetadata(
                Guid.NewGuid(),
                "task-completed",
                "v1",
                "management-service",
                "corr-management",
                "cause-management",
                DateTime.UtcNow),
            new ManagementTaskEventV1(
                Guid.NewGuid(),
                projectId,
                Guid.NewGuid(),
                userId,
                "task-completed",
                "high"));

        Assert.False(ingestResult.IsDuplicate);
        Assert.True(ingestResult.IsProjectionSupported);

        var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var userSummary = await dbContext.UserAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(summary => summary.UserId == userId);
        var projectSummary = await dbContext.ProjectAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(summary => summary.ProjectId == projectId);

        Assert.NotNull(userSummary);
        Assert.Equal(1, userSummary.TasksCompleted);
        Assert.Equal(0, userSummary.TotalXpDelta);

        Assert.NotNull(projectSummary);
        Assert.Equal(1, projectSummary.TasksCompleted);
        Assert.Equal(0, projectSummary.TotalXpDelta);
    }

    [Fact]
    public async Task GamificationProgressIngestion_ShouldRemainIdempotent()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var scope = _services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();
        var metadata = new EventMetadata(
            eventId,
            "reward-applied",
            "v1",
            "gamification-service",
            "corr-gamification",
            "cause-gamification",
            DateTime.UtcNow);
        var payload = new GamificationProgressEventV1(userId, 25, 10, 3, "TaskCompleted");

        var firstResult = await projectionService.IngestGamificationProgressAsync(metadata, payload);
        var secondResult = await projectionService.IngestGamificationProgressAsync(metadata, payload);

        var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var userSummary = await dbContext.UserAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(summary => summary.UserId == userId);

        Assert.False(firstResult.IsDuplicate);
        Assert.True(firstResult.IsProjectionSupported);
        Assert.True(secondResult.IsDuplicate);
        Assert.True(secondResult.IsProjectionSupported);
        Assert.NotNull(userSummary);
        Assert.Equal(25, userSummary.TotalXpDelta);
        Assert.Equal(10, userSummary.TotalCoinsDelta);
        Assert.Equal(3, userSummary.CurrentLevel);
    }

    [Fact]
    public async Task GamificationProgressIngestion_ShouldUpdateProjectSummary_WhenCausationPointsToManagementEvent()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskEventId = Guid.NewGuid();

        await using var scope = _services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();

        await projectionService.IngestManagementTaskAsync(
            new EventMetadata(
                taskEventId,
                "task-completed",
                "v1",
                "management-service",
                "corr-management",
                "cause-management",
                DateTime.UtcNow),
            new ManagementTaskEventV1(
                Guid.NewGuid(),
                projectId,
                Guid.NewGuid(),
                userId,
                "task-completed",
                "high"));

        var rewardMetadata = new EventMetadata(
            Guid.NewGuid(),
            "reward-applied",
            "v1",
            "gamification-service",
            "corr-management",
            taskEventId.ToString("N"),
            DateTime.UtcNow);

        await projectionService.IngestGamificationProgressAsync(
            rewardMetadata,
            new GamificationProgressEventV1(userId, 25, 10, 3, "TaskCompleted"));

        var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var projectSummary = await dbContext.ProjectAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(summary => summary.ProjectId == projectId);
        var rewardInboxEvent = await dbContext.AnalyticsInboxEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(evt => evt.EventId == rewardMetadata.EventId);

        Assert.NotNull(projectSummary);
        Assert.Equal(1, projectSummary.TasksCompleted);
        Assert.Equal(25, projectSummary.TotalXpDelta);
        Assert.Equal(10, projectSummary.TotalCoinsDelta);
        Assert.NotNull(rewardInboxEvent);
        Assert.Equal(projectId, rewardInboxEvent.ProjectId);
    }

    [Fact]
    public async Task UserSummary_ShouldReturnForbidden_WhenRequestTargetsAnotherUser()
    {
        var tokenUserId = Guid.NewGuid();
        var routeUserId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenUserId.ToString());

        var response = await _client.GetAsync($"/api/analytics/users/{routeUserId}/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProjectSummary_ShouldReturnForbidden_WhenSnapshotDeniesAccess()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());
        _server.SnapshotClient.Snapshot = new ManagementProjectAnalyticsSnapshot
        {
            ProjectId = projectId,
            WorkspaceId = Guid.NewGuid(),
            HasAccess = false
        };

        var response = await _client.GetAsync($"/api/analytics/projects/{projectId}/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ProjectOverview_ShouldReturnBucketsStatusDistributionAndMemberDirectory()
    {
        var userId = Guid.NewGuid();
        var teammateId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var todoStatusId = Guid.NewGuid();
        var doneStatusId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());

        _server.SnapshotClient.Snapshot = new ManagementProjectAnalyticsSnapshot
        {
            ProjectId = projectId,
            WorkspaceId = Guid.NewGuid(),
            HasAccess = true,
            StatusDistribution =
            [
                new ProjectStatusDistributionDto
                {
                    StatusId = todoStatusId,
                    Name = "Todo",
                    Order = 1,
                    TaskCount = 2
                },
                new ProjectStatusDistributionDto
                {
                    StatusId = doneStatusId,
                    Name = "Done",
                    Order = 2,
                    IsFinal = true,
                    TaskCount = 1
                }
            ],
            Members =
            [
                new ManagementProjectMemberSnapshotDto { UserId = userId, Role = "Owner" },
                new ManagementProjectMemberSnapshotDto { UserId = teammateId, Role = "Member" }
            ]
        };

        _server.UserLookupClient.Users[userId] = new UserLookupResult
        {
            UserId = userId,
            Username = "owner",
            DisplayName = "Owner User",
            AvatarUrl = "https://cdn.test/owner.png"
        };
        _server.UserLookupClient.Users[teammateId] = new UserLookupResult
        {
            UserId = teammateId,
            Username = "member",
            DisplayName = "Member User",
            AvatarUrl = "https://cdn.test/member.png"
        };

        await using var scope = _services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).AddHours(11);
        var today = DateTime.UtcNow.Date.AddHours(12);

        dbContext.ProjectAnalyticsSummaries.Add(new ProjectAnalyticsSummary
        {
            ProjectId = projectId,
            TasksCompleted = 2,
            TasksReopened = 1,
            SubtasksCompleted = 1,
            CommentsAdded = 1,
            TotalXpDelta = 120,
            TotalCoinsDelta = 35,
            UpdatedAtUtc = today
        });

        dbContext.AnalyticsInboxEvents.AddRange(
            new AnalyticsInboxEvent
            {
                EventId = Guid.NewGuid(),
                SourceService = "management-service",
                EventType = "task-completed",
                Metadata = "task-completed",
                ProjectId = projectId,
                OccurredAtUtc = yesterday
            },
            new AnalyticsInboxEvent
            {
                EventId = Guid.NewGuid(),
                SourceService = "management-service",
                EventType = "comment-added",
                Metadata = "comment-added",
                ProjectId = projectId,
                OccurredAtUtc = today
            },
            new AnalyticsInboxEvent
            {
                EventId = Guid.NewGuid(),
                SourceService = "management-service",
                EventType = "subtask-completed",
                Metadata = "subtask-completed",
                ProjectId = projectId,
                OccurredAtUtc = today
            });

        await dbContext.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/analytics/projects/{projectId}/overview?period=week");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProjectAnalyticsOverviewResponse>();

        Assert.NotNull(payload);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal(2, payload.Summary.TasksCompleted);
        Assert.Equal(2, payload.StatusDistribution.Count);
        Assert.Equal(2, payload.MemberDirectory.Count);
        Assert.Equal(7, payload.Productivity.Count);
        Assert.Contains(payload.MemberDirectory, member => member.UserId == userId && member.DisplayName == "Owner User");
        Assert.Contains(payload.StatusDistribution, status => status.Name == "Todo" && status.TaskCount == 2);
        Assert.Contains(payload.Productivity, bucket => bucket.TasksCompleted == 1);
        Assert.Contains(payload.Productivity, bucket => bucket.CommentsAdded == 1 && bucket.SubtasksCompleted == 1);
    }

    [Fact]
    public async Task ProjectOverview_ShouldFallbackToNullUserFields_WhenUserLookupFails()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userId.ToString());
        _server.SnapshotClient.Snapshot = new ManagementProjectAnalyticsSnapshot
        {
            ProjectId = projectId,
            WorkspaceId = Guid.NewGuid(),
            HasAccess = true,
            Members =
            [
                new ManagementProjectMemberSnapshotDto { UserId = userId, Role = "Owner" }
            ]
        };
        _server.UserLookupClient.ThrowOnLookup = true;

        var response = await _client.GetAsync($"/api/analytics/projects/{projectId}/overview?period=month");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ProjectAnalyticsOverviewResponse>();

        Assert.NotNull(payload);
        Assert.Single(payload.MemberDirectory);
        Assert.Equal(userId, payload.MemberDirectory[0].UserId);
        Assert.Null(payload.MemberDirectory[0].Username);
        Assert.Null(payload.MemberDirectory[0].DisplayName);
        Assert.Null(payload.MemberDirectory[0].AvatarUrl);
        Assert.Equal(30, payload.Productivity.Count);
    }

    [Fact]
    public async Task UnknownRoute_ShouldReturnNotFound()
    {
        var response = await _client.GetAsync("/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class FakeManagementProjectAnalyticsSnapshotClient : IManagementProjectAnalyticsSnapshotClient
    {
        public ManagementProjectAnalyticsSnapshot? Snapshot { get; set; }
        public bool ThrowOnLookup { get; set; }

        public Task<ManagementProjectAnalyticsSnapshot?> GetProjectAnalyticsSnapshotAsync(
            Guid projectId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnLookup)
            {
                throw new InvalidOperationException("Synthetic management snapshot failure.");
            }

            return Task.FromResult(Snapshot);
        }

        public void Reset()
        {
            Snapshot = null;
            ThrowOnLookup = false;
        }
    }

    public sealed class FakeUserLookupClient : IUserLookupClient
    {
        public Dictionary<Guid, UserLookupResult> Users { get; } = [];
        public bool ThrowOnLookup { get; set; }

        public Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnLookup)
            {
                throw new InvalidOperationException("Synthetic user lookup failure.");
            }

            IReadOnlyDictionary<Guid, UserLookupResult> result = Users
                .Where(entry => userIds.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value);

            return Task.FromResult(result);
        }

        public void Reset()
        {
            Users.Clear();
            ThrowOnLookup = false;
        }
    }

    public sealed class TestJwtAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestJwtAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(authorization) ||
                !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var token = authorization["Bearer ".Length..].Trim();
            if (!Guid.TryParse(token, out var userId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test token"));
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class ProjectAnalyticsOverviewResponse
    {
        public Guid ProjectId { get; set; }
        public ProjectAnalyticsSummaryResponse Summary { get; set; } = new();
        public List<ProjectProductivityBucketResponse> Productivity { get; set; } = [];
        public List<ProjectStatusDistributionResponse> StatusDistribution { get; set; } = [];
        public List<ProjectMemberDirectoryEntryResponse> MemberDirectory { get; set; } = [];
    }

    private sealed class ProjectAnalyticsSummaryResponse
    {
        public int TasksCompleted { get; set; }
        public int TasksReopened { get; set; }
        public int SubtasksCompleted { get; set; }
        public int CommentsAdded { get; set; }
        public long TotalXpDelta { get; set; }
        public long TotalCoinsDelta { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    private sealed class ProjectProductivityBucketResponse
    {
        public DateTime BucketStartUtc { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksReopened { get; set; }
        public int SubtasksCompleted { get; set; }
        public int CommentsAdded { get; set; }
    }

    private sealed class ProjectStatusDistributionResponse
    {
        public Guid StatusId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TaskCount { get; set; }
    }

    private sealed class ProjectMemberDirectoryEntryResponse
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
