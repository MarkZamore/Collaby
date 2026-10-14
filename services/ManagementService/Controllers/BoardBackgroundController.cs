using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ManagementService.Application.Interfaces;

namespace ManagementService.Controllers;

/// <summary>
/// API для выбора фона доски (поиск изображений через Unsplash).
/// Доступ: GET /api/management/images/search?q=ocean
/// </summary>
[ApiController]
[Route("images")]
[Authorize]
public class BoardBackgroundController : ControllerBase
{
    private readonly IUnsplashImageSearchService _imageSearchService;
    private readonly ILogger<BoardBackgroundController> _logger;

    public BoardBackgroundController(
        IUnsplashImageSearchService imageSearchService,
        ILogger<BoardBackgroundController> logger)
    {
        _imageSearchService = imageSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Поиск изображений для фона доски (прокси к Unsplash API).
    /// Требует настройки Unsplash:AccessKey в конфигурации.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int perPage = 12, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new { results = Array.Empty<object>() });
        }

        var results = await _imageSearchService.SearchAsync(q.Trim(), Math.Clamp(perPage, 1, 30), Math.Max(1, page), cancellationToken);
        return Ok(new { results = results.Select(r => new { r.Id, r.Url }) });
    }
}
