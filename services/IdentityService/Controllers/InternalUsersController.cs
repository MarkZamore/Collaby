using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.SystemAdministration;
using IdentityService.Infrastructure.Data;
using Collaby.InternalContracts.Events;

namespace IdentityService.Controllers;

[ApiController]
[Route("internal/v1/queries/users")]
public class InternalUsersController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly InternalServiceRequestAuthenticator _internalRequestAuthenticator;
    private readonly ISystemAdminRoster _systemAdminRoster;

    public InternalUsersController(
        IdentityDbContext dbContext,
        InternalServiceRequestAuthenticator internalRequestAuthenticator,
        ISystemAdminRoster systemAdminRoster)
    {
        _dbContext = dbContext;
        _internalRequestAuthenticator = internalRequestAuthenticator;
        _systemAdminRoster = systemAdminRoster;
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (!IsInternalCallAuthorized())
        {
            return Unauthorized(new
            {
                success = false,
                message = "Unauthorized"
            });
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new
            {
                success = false,
                message = "Query parameter 'q' is required"
            });
        }

        var normalizedQuery = q.Trim();
        var normalizedLimit = Math.Clamp(limit, 1, 50);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u =>
                EF.Functions.ILike(u.Username, $"%{normalizedQuery}%") ||
                EF.Functions.ILike(u.Email, $"%{normalizedQuery}%"))
            .OrderBy(u => u.Username)
            .Take(normalizedLimit)
            .Select(u => new InternalUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> Batch([FromBody] List<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (!IsInternalCallAuthorized())
        {
            return Unauthorized(new
            {
                success = false,
                message = "Unauthorized"
            });
        }

        var sanitizedIds = (userIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (sanitizedIds.Count == 0)
        {
            return Ok(Array.Empty<InternalUserDto>());
        }

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive && sanitizedIds.Contains(u.Id))
            .Select(u => new InternalUserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email
            })
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("directory/export")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportDirectory(
        [FromQuery] string? cursor,
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (!IsInternalCallAuthorized())
        {
            return Unauthorized(new
            {
                success = false,
                message = "Unauthorized"
            });
        }

        var normalizedLimit = Math.Clamp(limit, 1, 500);
        var offset = DecodeCursor(cursor);

        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Skip(offset)
            .Take(normalizedLimit + 1)
            .Select(u => new IdentityUserDirectoryEventV1(
                u.Id,
                u.Username,
                u.Email,
                u.IsActive,
                false,
                u.IsActive ? "activated" : "deactivated"))
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return Ok(new DirectoryExportResponse
            {
                Items = [],
                NextCursor = null
            });
        }

        var pageItems = users
            .Take(normalizedLimit)
            .Select(entry => entry with { IsSystemAdmin = _systemAdminRoster.IsSystemAdminEmail(entry.Email) })
            .ToArray();

        return Ok(new DirectoryExportResponse
        {
            Items = pageItems,
            NextCursor = users.Count > normalizedLimit ? EncodeCursor(offset + normalizedLimit) : null
        });
    }

    private bool IsInternalCallAuthorized()
    {
        return _internalRequestAuthenticator.Authenticate(Request).IsAuthorized;
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(raw, out var offset) && offset >= 0 ? offset : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
    }

    private sealed class DirectoryExportResponse
    {
        public IReadOnlyList<IdentityUserDirectoryEventV1> Items { get; init; } = [];
        public string? NextCursor { get; init; }
    }

    private sealed class InternalUserDto
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}

