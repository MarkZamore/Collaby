using GamificationService.Controllers;
using GamificationService.Contracts.Responses;
using GamificationService.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GamificationService.Tests.Unit;

public sealed class ProgressControllerTests
{
    [Fact]
    public async Task GetProjectProgress_WhenSnapshotIsMissing_ShouldReturnZeroedPayload()
    {
        await using var dbContext = CreateDbContext();
        var controller = new ProgressController(dbContext);
        var projectId = Guid.NewGuid();

        var result = await controller.GetProjectProgress(projectId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ProjectProgressResponse>(ok.Value);
        Assert.Equal(projectId, payload.ProjectId);
        Assert.Equal(0, payload.ProgressPercent);
        Assert.Equal(0, payload.TotalWeight);
        Assert.Equal(0, payload.CompletedWeight);
        Assert.Equal(0, payload.TotalTasks);
        Assert.Equal(0, payload.CompletedTasks);
        Assert.Equal(0, payload.TotalSubtasks);
        Assert.Equal(0, payload.CompletedSubtasks);
    }

    private static GamificationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GamificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new GamificationDbContext(options);
    }
}
