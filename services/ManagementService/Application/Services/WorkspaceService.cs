using Microsoft.EntityFrameworkCore;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Security;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

/// <summary>
/// Сервис для управления рабочими пространствами
/// </summary>
public class WorkspaceService : IWorkspaceService
{
    private const string OwnerRole = "Owner";
    private const string AdminRole = "Admin";
    private const string MemberRole = "Member";

    private readonly ManagementDbContext _dbContext;
    private readonly IUserLookupClient _userLookupClient;
    private readonly ILogger<WorkspaceService> _logger;

    public WorkspaceService(
        ManagementDbContext dbContext,
        IUserLookupClient userLookupClient,
        ILogger<WorkspaceService> logger)
    {
        _dbContext = dbContext;
        _userLookupClient = userLookupClient;
        _logger = logger;
    }

    /// <summary>
    /// Создать новое рабочее пространство
    /// </summary>
    public async Task<WorkspaceResponse> CreateWorkspaceAsync(string name, string? description, Guid userId)
    {
        try
        {
            // Создаём рабочее пространство
            var workspace = new Workspace
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = description,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsArchived = false
            };

            // Сразу добавляем создателя как Owner
            var ownerMember = new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = userId,
                Role = "Owner",
                Status = "Active",
                JoinedAt = DateTime.UtcNow
            };

            _dbContext.Workspaces.Add(workspace);
            _dbContext.WorkspaceMembers.Add(ownerMember);
            
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"Workspace '{name}' created successfully by user {userId}");

            var ownerResponses = await BuildMemberResponsesAsync(new List<WorkspaceMember> { ownerMember });
            return MapToWorkspaceResponse(workspace, ownerResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating workspace: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Получить все рабочие пространства пользователя
    /// </summary>
    public async Task<IEnumerable<WorkspaceResponse>> GetUserWorkspacesAsync(Guid userId)
    {
        try
        {
            var workspaces = await _dbContext.Workspaces
                .Where(w => w.Members.Any(m => m.UserId == userId && m.Status == "Active"))
                .Include(w => w.Members)
                .ToListAsync();

            var allMembers = workspaces.SelectMany(w => w.Members).ToList();
            var memberResponses = await BuildMemberResponsesAsync(allMembers);
            var memberMap = memberResponses.ToDictionary(r => r.Id);

            return workspaces.Select(w => MapToWorkspaceResponse(
                w,
                w.Members.Select(m => memberMap[m.Id]).ToList())).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting user workspaces: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Получить рабочее пространство по ID (с проверкой прав доступа)
    /// </summary>
    public async Task<WorkspaceResponse?> GetWorkspaceByIdAsync(Guid workspaceId, Guid userId)
    {
        try
        {
            // Проверяем, что пользователь является членом этого workspace
            var workspace = await _dbContext.Workspaces
                .Where(w => w.Id == workspaceId && w.Members.Any(m => m.UserId == userId))
                .Include(w => w.Members)
                .FirstOrDefaultAsync();

            if (workspace == null)
            {
                _logger.LogWarning($"Access denied: User {userId} tried to access workspace {workspaceId}");
                return null;
            }

            var memberResponses = await BuildMemberResponsesAsync(workspace.Members.ToList());
            return MapToWorkspaceResponse(workspace, memberResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error getting workspace by id: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Вспомогательный метод для преобразования сущности в DTO
    /// </summary>
    /// <summary>
    /// Пакетно загружает profile-данные для указанных участников и строит
    /// обогащённые экземпляры <see cref="WorkspaceMemberResponse"/>.
    /// Если UserService недоступен, поля профиля остаются null,
    /// но сами данные workspace всё равно возвращаются.
    /// </summary>
    private async Task<List<WorkspaceMemberResponse>> BuildMemberResponsesAsync(
        List<WorkspaceMember> members,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();

        if (members.Count > 0)
        {
            var userIds = members.Select(m => m.UserId).Distinct().ToList();
            try
            {
                profiles = await _userLookupClient.GetUsersByIdsAsync(userIds, cancellationToken);
            }
            catch (Exception ex)
            {
                // При недоступности UserService возвращаем workspace-данные без profile enrichment.
                _logger.LogWarning(ex, "Failed to fetch user profiles from UserService for {Count} user IDs", userIds.Count);
            }
        }

        return members.Select(m =>
        {
            profiles.TryGetValue(m.UserId, out var profile);
            return new WorkspaceMemberResponse
            {
                Id = m.Id,
                UserId = m.UserId,
                WorkspaceId = m.WorkspaceId,
                Role = m.Role,
                Status = m.Status,
                JoinedAt = m.JoinedAt,
                IsActive = m.Status == "Active",
                Username = profile?.Username,
                DisplayName = profile?.DisplayName,
                AvatarUrl = profile?.AvatarUrl
            };
        }).ToList();
    }

    private static WorkspaceResponse MapToWorkspaceResponse(Workspace workspace, List<WorkspaceMemberResponse> members)
    {
        return new WorkspaceResponse
        {
            Id = workspace.Id,
            Name = workspace.Name,
            Description = workspace.Description,
            OwnerId = workspace.OwnerId,
            CreatedAt = workspace.CreatedAt,
            UpdatedAt = workspace.UpdatedAt,
            IsArchived = workspace.IsArchived,
            Members = members
        };
    }

    /// <summary>
    /// Проверить доступ пользователя к рабочему пространству
    /// </summary>
    public async Task<bool> HasAccessToWorkspaceAsync(Guid workspaceId, Guid userId)
    {
        try
        {
            return await _dbContext.WorkspaceMembers
                .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.Status == "Active");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking workspace access: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Получить всех участников workspace
    /// </summary>
    public async Task<IEnumerable<WorkspaceMember>> GetWorkspaceMembersAsync(Guid workspaceId, Guid userId)
    {
        // Проверяем доступ к workspace
        if (!await HasAccessToWorkspaceAsync(workspaceId, userId))
        {
            throw new UnauthorizedAccessException("Access to workspace denied");
        }

        return await _dbContext.WorkspaceMembers
            .Where(m => m.WorkspaceId == workspaceId && m.Status == "Active")
            .OrderBy(m => m.JoinedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<WorkspaceMemberResponse>> GetWorkspaceMembersEnrichedAsync(
        Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        var members = (await GetWorkspaceMembersAsync(workspaceId, userId)).ToList();
        return await BuildMemberResponsesAsync(members, cancellationToken);
    }

    /// <summary>
    /// Добавить участника в workspace
    /// </summary>
    public async Task<AddWorkspaceMemberResult> AddMemberAsync(Guid workspaceId, Guid userIdToAdd, string role, Guid currentUserId)
    {
        await RequireMemberManagementPermissionAsync(workspaceId, currentUserId);

        // Проверяем, что пользователь еще не участник
        var existingMember = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userIdToAdd);

        if (existingMember != null)
        {
            if (existingMember.Status == "Active")
            {
                throw new InvalidOperationException("User is already a member");
            }

            if (existingMember.Status == "Pending")
            {
                // Повторный invite для Pending не должен создавать новое уведомление.
                existingMember.Role = role;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Resending invitation to user {userIdToAdd} in workspace {workspaceId}");
                return new AddWorkspaceMemberResult(existingMember, shouldSendInviteNotification: false);
            }
            
            // Повторно активируем приглашение, если пользователь раньше отклонил его или был удалён.
            existingMember.Status = "Pending";
            existingMember.Role = role;
            existingMember.JoinedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return new AddWorkspaceMemberResult(existingMember, shouldSendInviteNotification: true);
        }

        var member = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UserId = userIdToAdd,
            Role = role,
            Status = "Pending",
            JoinedAt = DateTime.UtcNow
        };

        _dbContext.WorkspaceMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"User {currentUserId} added user {userIdToAdd} to workspace {workspaceId} with role {role}");
        
        return new AddWorkspaceMemberResult(member, shouldSendInviteNotification: true);
    }

    /// <summary>
    /// Обновить роль участника
    /// </summary>
    public async Task<WorkspaceMember?> UpdateMemberRoleAsync(Guid workspaceId, Guid userIdToUpdate, string newRole, Guid currentUserId)
    {
        await RequireMemberManagementPermissionAsync(workspaceId, currentUserId);

        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userIdToUpdate);

        if (member == null)
        {
            return null;
        }

        // Нельзя изменить роль владельца
        if (member.Role == "Owner")
        {
            throw new InvalidOperationException("Cannot change Owner role");
        }

        // Проверяем, что не пытаемся назначить Owner (должна быть отдельная операция transfer ownership)
        if (newRole == "Owner")
        {
            throw new InvalidOperationException("Cannot assign Owner role. Use transfer ownership instead");
        }

        member.Role = newRole;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"User {currentUserId} updated role of user {userIdToUpdate} to {newRole} in workspace {workspaceId}");
        
        return member;
    }

    /// <summary>
    /// Удалить участника из workspace
    /// </summary>
    public async Task<bool> RemoveMemberAsync(Guid workspaceId, Guid userIdToRemove, Guid currentUserId)
    {
        await RequireMemberManagementPermissionAsync(workspaceId, currentUserId);

        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userIdToRemove);

        if (member == null)
        {
            return false;
        }

        // Нельзя удалить владельца
        if (member.Role == "Owner")
        {
            throw new InvalidOperationException("Cannot remove workspace Owner");
        }

        // Нельзя удалить самого себя (должна быть отдельная операция leave workspace)
        if (userIdToRemove == currentUserId)
        {
            throw new InvalidOperationException("Cannot remove yourself. Use leave workspace instead");
        }

        // Мягкое удаление - меняем статус
        member.Status = "Inactive";
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"User {currentUserId} removed user {userIdToRemove} from workspace {workspaceId}");
        
        return true;
    }

    /// <summary>
    /// Получить роль пользователя в workspace
    /// </summary>
    public async Task<string?> GetUserRoleAsync(Guid workspaceId, Guid userId)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.Status == "Active");

        return member?.Role;
    }

    /// <summary>
    /// Проверить, является ли пользователь владельцем или администратором
    /// </summary>
    public async Task<bool> IsOwnerOrAdminAsync(Guid workspaceId, Guid userId)
    {
        var role = await GetUserRoleAsync(workspaceId, userId);
        return IsOwnerOrAdminRole(role);
    }

    public Task RequireWorkspaceStructurePermissionAsync(Guid workspaceId, Guid userId)
    {
        return RequireCapabilityAsync(
            workspaceId,
            userId,
            static role => IsOwnerOrAdminRole(role),
            "workspace_structure_forbidden",
            "Недостаточно прав для управления структурой workspace");
    }

    public Task RequireTaskMutationPermissionAsync(Guid workspaceId, Guid userId)
    {
        return RequireCapabilityAsync(
            workspaceId,
            userId,
            static role => IsOwnerOrAdminRole(role) || IsRole(role, MemberRole),
            "task_mutation_forbidden",
            "Недостаточно прав для изменения задач и связанных сущностей");
    }

    public Task RequireMemberManagementPermissionAsync(Guid workspaceId, Guid userId)
    {
        return RequireCapabilityAsync(
            workspaceId,
            userId,
            static role => IsOwnerOrAdminRole(role),
            "member_management_forbidden",
            "Недостаточно прав для управления участниками workspace");
    }

    public Task RequireWorkspaceOwnershipAsync(Guid workspaceId, Guid userId)
    {
        return RequireCapabilityAsync(
            workspaceId,
            userId,
            static role => IsRole(role, OwnerRole),
            "workspace_owner_required",
            "Удаление workspace доступно только владельцу");
    }

    /// <summary>
    /// Удалить рабочее пространство целиком (только владелец)
    /// </summary>
    public async Task<bool> DeleteWorkspaceAsync(Guid workspaceId, Guid userId)
    {
        var workspace = await _dbContext.Workspaces
            .FirstOrDefaultAsync(w => w.Id == workspaceId);

        if (workspace == null)
            return false;

        await RequireWorkspaceOwnershipAsync(workspaceId, userId);

        _dbContext.Workspaces.Remove(workspace);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Принять приглашение — переводит Pending → Active
    /// </summary>
    public async Task<bool> AcceptInviteAsync(Guid workspaceId, Guid userId)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.Status == "Pending");
        if (member == null) return false;

        member.Status = "Active";
        member.JoinedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Отклонить приглашение — удаляет Pending-запись
    /// </summary>
    public async Task<bool> DeclineInviteAsync(Guid workspaceId, Guid userId)
    {
        var member = await _dbContext.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.Status == "Pending");
        if (member == null) return false;

        _dbContext.WorkspaceMembers.Remove(member);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    private async Task RequireCapabilityAsync(
        Guid workspaceId,
        Guid userId,
        Func<string, bool> rolePredicate,
        string code,
        string message)
    {
        var member = await _dbContext.WorkspaceMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId && m.Status == "Active");

        if (member == null)
        {
            throw new WorkspacePermissionException("workspace_access_forbidden", "Доступ к workspace запрещён");
        }

        if (!rolePredicate(member.Role))
        {
            throw new WorkspacePermissionException(code, message);
        }
    }

    private static bool IsOwnerOrAdminRole(string? role)
        => IsRole(role, OwnerRole) || IsRole(role, AdminRole);

    private static bool IsRole(string? actualRole, string expectedRole)
        => string.Equals(actualRole, expectedRole, StringComparison.OrdinalIgnoreCase);
}
