using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementService.Controllers;

[ApiController]
[Route("debug/health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new { status = "OK", service = "ManagementService!!!", timestamp = DateTime.UtcNow });
    }

    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtected()
    {
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
        return Ok(new { status = "OK", message = "Authenticated", userId, timestamp = DateTime.UtcNow });
    }
}
