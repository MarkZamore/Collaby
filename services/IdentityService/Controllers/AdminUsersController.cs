using System.Security.Claims;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Contracts.Responses.Admin;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Collaby.ServiceDefaults.SystemAdministration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Controllers;

[ApiController]
[Authorize(Policy = SystemAdminConstants.PolicyName)]
[Route("admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IdentityDbContext _dbContext;
    private readonly ISystemAdminRoster _systemAdminRoster;
    private readonly IUserDeletionOrchestrator _userDeletionOrchestrator;
    private readonly IIdentityDirectoryOutboxWriter _identityDirectoryOutboxWriter;

    public AdminUsersController(
        IdentityDbContext dbContext,
        ISystemAdminRoster systemAdminRoster,
        IUserDeletionOrchestrator userDeletionOrchestrator,
        IIdentityDirectoryOutboxWriter identityDirectoryOutboxWriter)
    {
        _dbContext = dbContext;
        _systemAdminRoster = systemAdminRoster;
        _userDeletionOrchestrator = userDeletionOrchestrator;
        _identityDirectoryOutboxWriter = identityDirectoryOutboxWriter;
    }

    [HttpGet("")]
    public async Task<ActionResult<AdminUserListResponse>> GetUsers(
        [FromQuery] string? q,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isSystemAdmin,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedQuery = q?.Trim();

        var query = _dbContext.Users
            .AsNoTracking()
            .Include(user => user.TwoFactorSetting)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(user =>
                EF.Functions.ILike(user.Email, $"%{normalizedQuery}%") ||
                EF.Functions.ILike(user.Username, $"%{normalizedQuery}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(user => user.IsActive == isActive.Value);
        }

        if (isSystemAdmin.HasValue)
        {
            var rosterEmails = _systemAdminRoster.GetEmails();
            query = isSystemAdmin.Value
                ? query.Where(user => rosterEmails.Contains(user.Email.ToLower()))
                : query.Where(user => !rosterEmails.Contains(user.Email.ToLower()));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(user => user.Id).ToArray();
        var latestOperations = await _dbContext.UserDeletionOperations
            .AsNoTracking()
            .Where(operation => userIds.Contains(operation.UserId))
            .OrderByDescending(operation => operation.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        var latestOperationByUser = latestOperations
            .GroupBy(operation => operation.UserId)
            .ToDictionary(group => group.Key, group => group.First());

        return Ok(new AdminUserListResponse
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            Items = users.Select(user =>
            {
                latestOperationByUser.TryGetValue(user.Id, out var operation);
                return MapListItem(user, operation);
            }).ToArray()
        });
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUser(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(entity => entity.TwoFactorSetting)
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { code = "user_not_found", message = "User not found." });
        }

        var operation = await _dbContext.UserDeletionOperations
            .AsNoTracking()
            .Include(entity => entity.Steps)
            .Where(entity => entity.UserId == userId)
            .OrderByDescending(entity => entity.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(MapDetail(user, operation));
    }

    [HttpGet("system-admin-roster")]
    public async Task<ActionResult<SystemAdminRosterResponse>> GetSystemAdminRoster(CancellationToken cancellationToken = default)
    {
        var rosterEmails = _systemAdminRoster.GetEmails();
        var registeredUsers = await _dbContext.Users
            .AsNoTracking()
            .Where(user => rosterEmails.Contains(user.Email.ToLower()))
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.Username,
                user.IsActive
            })
            .ToListAsync(cancellationToken);

        var registeredByEmail = registeredUsers.ToDictionary(
            user => user.Email.Trim().ToLowerInvariant(),
            user => user);

        return Ok(new SystemAdminRosterResponse
        {
            Entries = rosterEmails
                .OrderBy(email => email)
                .Select(email =>
                {
                    registeredByEmail.TryGetValue(email, out var user);
                    return new SystemAdminRosterEntryResponse
                    {
                        Email = email,
                        IsRegistered = user is not null,
                        UserId = user?.Id,
                        Username = user?.Username,
                        IsActive = user?.IsActive
                    };
                })
                .ToArray()
        });
    }

    [HttpPost("{userId:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(entity => entity.RefreshTokens)
            .Include(entity => entity.TwoFactorSetting)
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { code = "user_not_found", message = "User not found." });
        }

        if (IsProtected(user))
        {
            return Conflict(new { code = "protected_account", message = "Protected system admin accounts cannot be deactivated." });
        }

        user.IsActive = false;
        foreach (var refreshToken in user.RefreshTokens.Where(token => !token.RevokedAt.HasValue))
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
        }

        _identityDirectoryOutboxWriter.Enqueue(user, "deactivated");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(user, null));
    }

    [HttpPost("{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(entity => entity.TwoFactorSetting)
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { code = "user_not_found", message = "User not found." });
        }

        var hasDeletionHistory = await _dbContext.UserDeletionOperations
            .AsNoTracking()
            .AnyAsync(operation => operation.UserId == userId, cancellationToken);

        if (hasDeletionHistory)
        {
            return Conflict(new { code = "hard_delete_started", message = "Users with hard delete history cannot be reactivated." });
        }

        user.IsActive = true;
        _identityDirectoryOutboxWriter.Enqueue(user, "activated");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(user, null));
    }

    [HttpPost("{userId:guid}/hard-delete")]
    public async Task<IActionResult> HardDelete(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken);

        if (user is null)
        {
            return NotFound(new { code = "user_not_found", message = "User not found." });
        }

        if (IsProtected(user))
        {
            return Conflict(new { code = "protected_account", message = "Protected system admin accounts cannot be hard-deleted." });
        }

        try
        {
            var operation = await _userDeletionOrchestrator.StartDeletionAsync(
                userId,
                GetCurrentUserId(),
                GetCurrentUserEmail(),
                cancellationToken);

            var reloaded = await _userDeletionOrchestrator.GetOperationAsync(operation.Id, cancellationToken);
            return AcceptedAtAction(
                nameof(GetDeletionOperation),
                new { operationId = operation.Id },
                MapOperation(reloaded ?? operation));
        }
        catch (AdminUserGovernanceException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("deletion-operations/{operationId:guid}")]
    public async Task<IActionResult> GetDeletionOperation(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _userDeletionOrchestrator.GetOperationAsync(operationId, cancellationToken);
        if (operation is null)
        {
            return NotFound(new { code = "operation_not_found", message = "Deletion operation not found." });
        }

        return Ok(MapOperation(operation));
    }

    [HttpPost("deletion-operations/{operationId:guid}/retry")]
    public async Task<IActionResult> RetryDeletionOperation(Guid operationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var operation = await _userDeletionOrchestrator.RetryAsync(operationId, cancellationToken);
            var reloaded = await _userDeletionOrchestrator.GetOperationAsync(operation.Id, cancellationToken);
            return Ok(MapOperation(reloaded ?? operation));
        }
        catch (AdminUserGovernanceException ex)
        {
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    private AdminUserListItemResponse MapListItem(User user, UserDeletionOperation? operation)
    {
        var isSystemAdmin = _systemAdminRoster.IsSystemAdminEmail(user.Email);
        return new AdminUserListItemResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsActive = user.IsActive,
            EmailVerified = user.IsEmailVerified,
            TwoFactorEnabled = user.TwoFactorSetting?.Enabled ?? false,
            IsSystemAdmin = isSystemAdmin,
            IsProtectedAccount = isSystemAdmin,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            DeletionOperationId = operation?.Id,
            DeletionStatus = operation?.Status
        };
    }

    private AdminUserDetailResponse MapDetail(User user, UserDeletionOperation? operation)
    {
        var isSystemAdmin = _systemAdminRoster.IsSystemAdminEmail(user.Email);
        return new AdminUserDetailResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            IsActive = user.IsActive,
            EmailVerified = user.IsEmailVerified,
            TwoFactorEnabled = user.TwoFactorSetting?.Enabled ?? false,
            IsSystemAdmin = isSystemAdmin,
            IsProtectedAccount = isSystemAdmin,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            DeletionOperation = operation is null ? null : MapOperation(operation)
        };
    }

    private static UserDeletionOperationResponse MapOperation(UserDeletionOperation operation)
    {
        return new UserDeletionOperationResponse
        {
            Id = operation.Id,
            UserId = operation.UserId,
            UserEmail = operation.UserEmail,
            Status = operation.Status,
            CorrelationId = operation.CorrelationId,
            RequestedAtUtc = operation.RequestedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc,
            StartedAtUtc = operation.StartedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            FailureMessage = operation.FailureMessage,
            RetryCount = operation.RetryCount,
            CanRetry = UserDeletionRecoveryPolicy.CanRetry(operation),
            Steps = operation.Steps
                .OrderBy(step => step.StepOrder)
                .Select(step => new UserDeletionOperationStepResponse
                {
                    ServiceName = step.ServiceName,
                    StepOrder = step.StepOrder,
                    Status = step.Status,
                    AttemptCount = step.AttemptCount,
                    StartedAtUtc = step.StartedAtUtc,
                    CompletedAtUtc = step.CompletedAtUtc,
                    LastError = step.LastError
                })
                .ToArray()
        };
    }

    private bool IsProtected(User user)
    {
        return _systemAdminRoster.IsSystemAdminEmail(user.Email);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private string? GetCurrentUserEmail()
    {
        return User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
    }
}
