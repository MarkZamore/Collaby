using System.Security.Claims;
using AnalyticsService.Application.Contracts;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Domain.Entities;
using AnalyticsService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Controllers;

[ApiController]
[Authorize]
[Route("")]
public sealed class AnalyticsSummaryController : ControllerBase
{
    private readonly AnalyticsDbContext _dbContext;
    private readonly IManagementProjectAnalyticsSnapshotClient _managementSnapshotClient;
    private readonly IProjectAnalyticsOverviewService _projectAnalyticsOverviewService;
    private readonly ILogger<AnalyticsSummaryController> _logger;

    public AnalyticsSummaryController(
        AnalyticsDbContext dbContext,
        IManagementProjectAnalyticsSnapshotClient managementSnapshotClient,
        IProjectAnalyticsOverviewService projectAnalyticsOverviewService,
        ILogger<AnalyticsSummaryController> logger)
    {
        _dbContext = dbContext;
        _managementSnapshotClient = managementSnapshotClient;
        _projectAnalyticsOverviewService = projectAnalyticsOverviewService;
        _logger = logger;
    }

    [HttpGet("users/{userId:guid}/summary")]
    public async Task<IActionResult> GetUserSummary(Guid userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Authenticated user identifier is missing" });
        }

        if (currentUserId.Value != userId)
        {
            return Forbid();
        }

        var summary = await _dbContext.UserAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (summary is null)
        {
            return NotFound(new { message = "User analytics summary not found" });
        }

        return Ok(MapUserSummary(summary));
    }

    [HttpGet("projects/{projectId:guid}/summary")]
    public async Task<IActionResult> GetProjectSummary(Guid projectId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Authenticated user identifier is missing" });
        }

        var authorizationResult = await AuthorizeProjectAsync(projectId, currentUserId.Value, cancellationToken);
        if (authorizationResult.Error is not null)
        {
            return authorizationResult.Error;
        }

        var summary = await _dbContext.ProjectAnalyticsSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

        if (summary is null)
        {
            return NotFound(new { message = "Project analytics summary not found" });
        }

        return Ok(MapProjectSummary(summary));
    }

    [HttpGet("projects/{projectId:guid}/overview")]
    public async Task<IActionResult> GetProjectOverview(
        Guid projectId,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized(new { message = "Authenticated user identifier is missing" });
        }

        if (!TryParsePeriod(period, out var normalizedPeriod))
        {
            return BadRequest(new { message = "Supported values for 'period' are 'week' and 'month'." });
        }

        var authorizationResult = await AuthorizeProjectAsync(projectId, currentUserId.Value, cancellationToken);
        if (authorizationResult.Error is not null)
        {
            return authorizationResult.Error;
        }

        var overview = await _projectAnalyticsOverviewService.BuildOverviewAsync(
            projectId,
            authorizationResult.Snapshot!,
            normalizedPeriod,
            cancellationToken);

        return Ok(overview);
    }

    private async Task<ProjectAuthorizationResult> AuthorizeProjectAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _managementSnapshotClient.GetProjectAnalyticsSnapshotAsync(
                projectId,
                userId,
                cancellationToken);

            if (snapshot is null)
            {
                return ProjectAuthorizationResult.FromError(
                    NotFound(new { message = "Project analytics snapshot not found" }));
            }

            if (!snapshot.HasAccess)
            {
                return ProjectAuthorizationResult.FromError(Forbid());
            }

            return ProjectAuthorizationResult.FromSnapshot(snapshot);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Project analytics authorization query failed for project {ProjectId}", projectId);
            return ProjectAuthorizationResult.FromError(
                StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Project analytics dependencies are temporarily unavailable"
                }));
        }
    }

    private Guid? GetCurrentUserId()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawUserId, out var userId) ? userId : null;
    }

    private static bool TryParsePeriod(string? rawValue, out ProjectAnalyticsPeriod period)
    {
        if (string.IsNullOrWhiteSpace(rawValue) || rawValue.Equals("week", StringComparison.OrdinalIgnoreCase))
        {
            period = ProjectAnalyticsPeriod.Week;
            return true;
        }

        if (rawValue.Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            period = ProjectAnalyticsPeriod.Month;
            return true;
        }

        period = default;
        return false;
    }

    private static ProjectAnalyticsSummaryDto MapProjectSummary(ProjectAnalyticsSummary summary)
    {
        return new ProjectAnalyticsSummaryDto
        {
            TasksCompleted = summary.TasksCompleted,
            TasksReopened = summary.TasksReopened,
            SubtasksCompleted = summary.SubtasksCompleted,
            CommentsAdded = summary.CommentsAdded,
            TotalXpDelta = summary.TotalXpDelta,
            TotalCoinsDelta = summary.TotalCoinsDelta,
            UpdatedAtUtc = summary.UpdatedAtUtc
        };
    }

    private static UserAnalyticsSummaryDto MapUserSummary(UserAnalyticsSummary summary)
    {
        return new UserAnalyticsSummaryDto
        {
            TasksCompleted = summary.TasksCompleted,
            TasksReopened = summary.TasksReopened,
            SubtasksCompleted = summary.SubtasksCompleted,
            CommentsAdded = summary.CommentsAdded,
            TotalXpDelta = summary.TotalXpDelta,
            TotalCoinsDelta = summary.TotalCoinsDelta,
            CurrentLevel = summary.CurrentLevel,
            UpdatedAtUtc = summary.UpdatedAtUtc
        };
    }

    private sealed class ProjectAuthorizationResult
    {
        public IActionResult? Error { get; init; }
        public ManagementProjectAnalyticsSnapshot? Snapshot { get; init; }

        public static ProjectAuthorizationResult FromError(IActionResult error)
            => new() { Error = error };

        public static ProjectAuthorizationResult FromSnapshot(ManagementProjectAnalyticsSnapshot snapshot)
            => new() { Snapshot = snapshot };
    }

    private sealed class UserAnalyticsSummaryDto
    {
        public int TasksCompleted { get; init; }
        public int TasksReopened { get; init; }
        public int SubtasksCompleted { get; init; }
        public int CommentsAdded { get; init; }
        public long TotalXpDelta { get; init; }
        public long TotalCoinsDelta { get; init; }
        public int? CurrentLevel { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
