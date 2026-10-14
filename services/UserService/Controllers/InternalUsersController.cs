using Collaby.ServiceDefaults.InternalAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Infrastructure.Data;

namespace UserService.Controllers;

[ApiController]
[Route("internal/v1/queries/users")]
public class InternalUsersController : ControllerBase
{
    private readonly UserDbContext _dbContext;
    private readonly InternalServiceRequestAuthenticator _internalRequestAuthenticator;

    public InternalUsersController(
        UserDbContext dbContext,
        InternalServiceRequestAuthenticator internalRequestAuthenticator)
    {
        _dbContext = dbContext;
        _internalRequestAuthenticator = internalRequestAuthenticator;
    }

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> Batch(
        [FromBody] List<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        var sanitized = (userIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (sanitized.Count == 0)
        {
            return Ok(Array.Empty<InternalUserProfileDto>());
        }

        var profiles = await _dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => sanitized.Contains(profile.UserId))
            .Select(profile => new { profile.UserId, profile.DisplayName, profile.AvatarUrl })
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

        var identityMap = await _dbContext.IdentityDirectoryEntries
            .AsNoTracking()
            .Where(entry => entry.IsActive && sanitized.Contains(entry.UserId))
            .ToDictionaryAsync(entry => entry.UserId, cancellationToken);

        var result = sanitized.Select(userId =>
        {
            profiles.TryGetValue(userId, out var profile);
            identityMap.TryGetValue(userId, out var identity);

            return new InternalUserProfileDto
            {
                UserId = userId,
                Username = identity?.Username,
                Email = identity?.Email,
                DisplayName = profile?.DisplayName,
                AvatarUrl = profile?.AvatarUrl
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthorized())
        {
            return Unauthorized(new { success = false, message = "Unauthorized" });
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { success = false, message = "Query parameter 'q' is required" });
        }

        var normalizedLimit = Math.Clamp(limit, 1, 50);
        var normalizedQuery = q.Trim().ToLowerInvariant();

        var identityUsers = await _dbContext.IdentityDirectoryEntries
            .AsNoTracking()
            .Where(entry =>
                entry.IsActive &&
                (entry.Username.ToLower().Contains(normalizedQuery) || entry.Email.ToLower().Contains(normalizedQuery)))
            .OrderBy(entry => entry.Username)
            .Take(normalizedLimit)
            .ToListAsync(cancellationToken);

        if (identityUsers.Count == 0)
        {
            return Ok(Array.Empty<InternalUserProfileDto>());
        }

        var userIds = identityUsers.Select(entry => entry.UserId).ToList();

        var profiles = await _dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .Select(profile => new { profile.UserId, profile.DisplayName, profile.AvatarUrl })
            .ToDictionaryAsync(profile => profile.UserId, cancellationToken);

        var result = identityUsers.Select(entry =>
        {
            profiles.TryGetValue(entry.UserId, out var profile);

            return new InternalUserProfileDto
            {
                UserId = entry.UserId,
                Username = entry.Username,
                Email = entry.Email,
                DisplayName = profile?.DisplayName,
                AvatarUrl = profile?.AvatarUrl
            };
        }).ToList();

        return Ok(result);
    }

    private bool IsAuthorized()
    {
        return _internalRequestAuthenticator.Authenticate(Request).IsAuthorized;
    }

    private sealed class InternalUserProfileDto
    {
        public Guid UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
