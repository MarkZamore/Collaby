using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using ManagementService.Application.Events;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public class InvitationService : IInvitationService
{
    private readonly ManagementDbContext _dbContext;
    private readonly IWorkspaceService _workspaceService;
    private readonly IUserLookupClient _userLookupClient;
    private readonly IEmailQueueService _emailQueueService;
    private readonly ILogger<InvitationService> _logger;

    public InvitationService(
        ManagementDbContext dbContext,
        IWorkspaceService workspaceService,
        IUserLookupClient userLookupClient,
        IEmailQueueService emailQueueService,
        ILogger<InvitationService> logger)
    {
        _dbContext = dbContext;
        _workspaceService = workspaceService;
        _userLookupClient = userLookupClient;
        _emailQueueService = emailQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Создать приглашение в workspace
    /// </summary>
    public async Task<WorkspaceInvitation> CreateInvitationAsync(
        Guid workspaceId,
        string email,
        string role,
        int expiresInDays,
        Guid invitedBy)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!await _workspaceService.IsOwnerOrAdminAsync(workspaceId, invitedBy))
        {
            throw new UnauthorizedAccessException("Only Owner or Admin can create invitations");
        }

        if (await ResolveActiveMemberByExactEmailAsync(workspaceId, normalizedEmail))
        {
            await RevokePendingInvitationsForEmailAsync(workspaceId, normalizedEmail);
            throw new InvalidOperationException("This user is already an active member of the workspace");
        }

        var existingInvitation = await _dbContext.WorkspaceInvitations
            .FirstOrDefaultAsync(i => i.WorkspaceId == workspaceId
                && i.Email.ToLower() == normalizedEmail
                && i.Status == "Pending"
                && i.ExpiresAt > DateTime.UtcNow);

        if (existingInvitation != null)
        {
            throw new InvalidOperationException("Active invitation already exists for this email");
        }

        var token = GenerateSecureToken();

        var invitation = new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Email = normalizedEmail,
            Role = role,
            Token = token,
            InvitedBy = invitedBy,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
            Status = "Pending"
        };

        _dbContext.WorkspaceInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        var workspace = await _dbContext.Workspaces.FindAsync(workspaceId);
        invitation.Workspace = workspace;

        await _emailQueueService.QueueInvitationEmailAsync(new InvitationEmailEvent
        {
            Email = normalizedEmail,
            WorkspaceName = workspace?.Name ?? "Unknown Workspace",
            InviterName = invitedBy.ToString(),
            InvitationToken = token,
            Role = role,
            ExpiresAt = invitation.ExpiresAt
        });

        _logger.LogInformation(
            "Invitation created for {Email} to workspace {WorkspaceId} with role {Role}",
            normalizedEmail,
            workspaceId,
            role);

        return invitation;
    }

    /// <summary>
    /// Принять приглашение
    /// </summary>
    public async Task<WorkspaceMember> AcceptInvitationAsync(string token, Guid userId)
    {
        var invitation = await _dbContext.WorkspaceInvitations
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation == null)
        {
            throw new InvalidOperationException("Invitation not found");
        }

        if (invitation.Status != "Pending")
        {
            throw new InvalidOperationException($"Invitation is {invitation.Status}");
        }

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "Expired";
            await _dbContext.SaveChangesAsync();
            throw new InvalidOperationException("Invitation has expired");
        }

        var existingMember = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == invitation.WorkspaceId && m.UserId == userId);

        if (existingMember != null && existingMember.Status == "Active")
        {
            throw new InvalidOperationException("You are already a member of this workspace");
        }

        WorkspaceMember member;

        if (existingMember != null)
        {
            existingMember.Status = "Active";
            existingMember.Role = invitation.Role;
            existingMember.JoinedAt = DateTime.UtcNow;
            member = existingMember;
        }
        else
        {
            member = new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = invitation.WorkspaceId,
                UserId = userId,
                Role = invitation.Role,
                Status = "Active",
                JoinedAt = DateTime.UtcNow,
                InvitedBy = invitation.InvitedBy
            };
            _dbContext.WorkspaceMembers.Add(member);
        }

        invitation.Status = "Accepted";
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedBy = userId;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} accepted invitation to workspace {WorkspaceId}", userId, invitation.WorkspaceId);

        return member;
    }

    /// <summary>
    /// Отозвать приглашение
    /// </summary>
    public async Task<bool> RevokeInvitationAsync(Guid invitationId, Guid userId)
    {
        var invitation = await _dbContext.WorkspaceInvitations.FindAsync(invitationId);

        if (invitation == null)
        {
            return false;
        }

        if (!await _workspaceService.IsOwnerOrAdminAsync(invitation.WorkspaceId, userId))
        {
            throw new UnauthorizedAccessException("Only Owner or Admin can revoke invitations");
        }

        if (invitation.Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot revoke invitation with status {invitation.Status}");
        }

        invitation.Status = "Revoked";
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} revoked invitation {InvitationId}", userId, invitationId);

        return true;
    }

    /// <summary>
    /// Получить приглашения workspace
    /// </summary>
    public async Task<IEnumerable<WorkspaceInvitation>> GetWorkspaceInvitationsAsync(Guid workspaceId, Guid userId)
    {
        if (!await _workspaceService.HasAccessToWorkspaceAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Access to workspace denied");
        }

        return await _dbContext.WorkspaceInvitations
            .Include(i => i.Workspace)
            .Where(i => i.WorkspaceId == workspaceId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить приглашение по токену
    /// </summary>
    public async Task<WorkspaceInvitation?> GetInvitationByTokenAsync(string token)
    {
        return await _dbContext.WorkspaceInvitations
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token);
    }

    public async Task<bool> IsInvitationAlreadySatisfiedByUserAsync(Guid workspaceId, string invitationEmail, Guid userId)
    {
        var isActiveMember = await _dbContext.WorkspaceMembers.AnyAsync(member =>
            member.WorkspaceId == workspaceId &&
            member.UserId == userId &&
            member.Status == "Active");

        if (!isActiveMember)
        {
            return false;
        }

        try
        {
            var users = await _userLookupClient.GetUsersByIdsAsync([userId]);
            if (!users.TryGetValue(userId, out var user))
            {
                return false;
            }

            return string.Equals(
                user.Email?.Trim(),
                invitationEmail?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve current user email for invitation preview in workspace {WorkspaceId}",
                workspaceId);
            return false;
        }
    }

    /// <summary>
    /// Генерация безопасного токена
    /// </summary>
    private string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private async Task<bool> ResolveActiveMemberByExactEmailAsync(Guid workspaceId, string normalizedEmail)
    {
        try
        {
            var matches = await _userLookupClient.SearchUsersAsync(normalizedEmail, 10);
            var matchedUser = matches.FirstOrDefault(user =>
                string.Equals(user.Email?.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

            if (matchedUser == null)
            {
                return false;
            }

            return await _dbContext.WorkspaceMembers.AnyAsync(member =>
                member.WorkspaceId == workspaceId &&
                member.UserId == matchedUser.UserId &&
                member.Status == "Active");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve exact email match for invitation deduplication in workspace {WorkspaceId}",
                workspaceId);
            return false;
        }
    }

    private async Task RevokePendingInvitationsForEmailAsync(Guid workspaceId, string normalizedEmail)
    {
        var pendingInvitations = await _dbContext.WorkspaceInvitations
            .Where(invitation =>
                invitation.WorkspaceId == workspaceId &&
                invitation.Email.ToLower() == normalizedEmail &&
                invitation.Status == "Pending")
            .ToListAsync();

        if (pendingInvitations.Count == 0)
        {
            return;
        }

        foreach (var invitation in pendingInvitations)
        {
            invitation.Status = "Revoked";
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Revoked {Count} stale pending invitations for {Email} in workspace {WorkspaceId}",
            pendingInvitations.Count,
            normalizedEmail,
            workspaceId);
    }
}
