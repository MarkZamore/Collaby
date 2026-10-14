using GamificationService.Contracts.Responses;
using GamificationService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Controllers;

[ApiController]
[Route("progress")]
[Authorize]
/// <summary>
/// Отдаёт прогресс по проекту из локальной projection, которую GamificationService синхронизирует через Kafka.
/// </summary>
public class ProgressController : ControllerBase
{
    private readonly GamificationDbContext _dbContext;

    public ProgressController(GamificationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("project/{projectId:guid}")]
    public async Task<IActionResult> GetProjectProgress(Guid projectId)
    {
        var snapshot = await _dbContext.ProjectProgressSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.ProjectId == projectId);

        if (snapshot is null)
        {
            return Ok(new ProjectProgressResponse
            {
                ProjectId = projectId,
                ProgressPercent = 0,
                TotalWeight = 0,
                CompletedWeight = 0,
                TotalTasks = 0,
                CompletedTasks = 0,
                TotalSubtasks = 0,
                CompletedSubtasks = 0
            });
        }

        return Ok(new ProjectProgressResponse
        {
            ProjectId = snapshot.ProjectId,
            ProgressPercent = snapshot.ProgressPercent,
            TotalWeight = snapshot.TotalWeight,
            CompletedWeight = snapshot.CompletedWeight,
            TotalTasks = snapshot.TotalTasks,
            CompletedTasks = snapshot.CompletedTasks,
            TotalSubtasks = snapshot.TotalSubtasks,
            CompletedSubtasks = snapshot.CompletedSubtasks
        });
    }
}
