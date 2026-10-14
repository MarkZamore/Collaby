using Collaby.ServiceDefaults.Correlation;
using Collaby.InternalContracts.InternalAdmin;
using Collaby.ServiceDefaults.InternalAuth;
using GamificationService.Application.Interfaces;
using GamificationService.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Controllers;

[ApiController]
[Route("internal")]
public class InternalController : ControllerBase
{
    private readonly GamificationDbContext _dbContext;
    private readonly ILeaderboardService _leaderboardService;
    private readonly InternalServiceRequestAuthenticator _internalRequestAuthenticator;

    public InternalController(
        GamificationDbContext dbContext,
        ILeaderboardService leaderboardService,
        InternalServiceRequestAuthenticator internalRequestAuthenticator)
    {
        _dbContext = dbContext;
        _leaderboardService = leaderboardService;
        _internalRequestAuthenticator = internalRequestAuthenticator;
    }

    [HttpPost("snapshot-annual")]
    public async Task<IActionResult> CreateAnnualSnapshot([FromQuery] int? year)
    {
        if (!_internalRequestAuthenticator.Authenticate(Request).IsAuthorized)
        {
            return Unauthorized(new { error = "Invalid internal caller credentials" });
        }

        var snapshotYear = year ?? DateTime.UtcNow.Year;
        await _leaderboardService.CreateAnnualSnapshotAsync(snapshotYear);
        return Ok(new { message = $"Annual snapshot created for {snapshotYear}" });
    }

}
