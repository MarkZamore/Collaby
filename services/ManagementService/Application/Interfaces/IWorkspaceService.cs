using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

public sealed class AddWorkspaceMemberResult
{
    public AddWorkspaceMemberResult(WorkspaceMember member, bool shouldSendInviteNotification)
    {
        Member = member;
        ShouldSendInviteNotification = shouldSendInviteNotification;
    }

    public WorkspaceMember Member { get; }

    public bool ShouldSendInviteNotification { get; }
}

/// <summary>
/// Интерфейс для работы с рабочими пространствами
/// </summary>
public interface IWorkspaceService
{
    /// <summary>
    /// Создать новое рабочее пространство
    /// </summary>
    Task<WorkspaceResponse> CreateWorkspaceAsync(string name, string? description, Guid userId);

    /// <summary>
    /// Получить все рабочие пространства пользователя
    /// </summary>
    Task<IEnumerable<WorkspaceResponse>> GetUserWorkspacesAsync(Guid userId);

    /// <summary>
    /// Получить рабочее пространство по ID (с проверкой прав доступа)
    /// </summary>
    Task<WorkspaceResponse?> GetWorkspaceByIdAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Проверить доступ пользователя к рабочему пространству
    /// </summary>
    Task<bool> HasAccessToWorkspaceAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Получить всех участников workspace (raw entities)
    /// </summary>
    Task<IEnumerable<WorkspaceMember>> GetWorkspaceMembersAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Получить всех участников workspace с обогащёнными данными профиля из UserService
    /// </summary>
    Task<List<WorkspaceMemberResponse>> GetWorkspaceMembersEnrichedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавить участника в workspace
    /// </summary>
    Task<AddWorkspaceMemberResult> AddMemberAsync(Guid workspaceId, Guid userIdToAdd, string role, Guid currentUserId);

    /// <summary>
    /// Обновить роль участника
    /// </summary>
    Task<WorkspaceMember?> UpdateMemberRoleAsync(Guid workspaceId, Guid userIdToUpdate, string newRole, Guid currentUserId);

    /// <summary>
    /// Удалить участника из workspace
    /// </summary>
    Task<bool> RemoveMemberAsync(Guid workspaceId, Guid userIdToRemove, Guid currentUserId);

    /// <summary>
    /// Получить роль пользователя в workspace
    /// </summary>
    Task<string?> GetUserRoleAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Проверить, является ли пользователь владельцем или администратором
    /// </summary>
    Task<bool> IsOwnerOrAdminAsync(Guid workspaceId, Guid userId);

    Task RequireWorkspaceStructurePermissionAsync(Guid workspaceId, Guid userId);

    Task RequireTaskMutationPermissionAsync(Guid workspaceId, Guid userId);

    Task RequireMemberManagementPermissionAsync(Guid workspaceId, Guid userId);

    Task RequireWorkspaceOwnershipAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Удалить рабочее пространство (только владелец)
    /// </summary>
    Task<bool> DeleteWorkspaceAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Принять приглашение в workspace (переводит Pending → Active)
    /// </summary>
    Task<bool> AcceptInviteAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Отклонить приглашение в workspace (удаляет Pending-запись)
    /// </summary>
    Task<bool> DeclineInviteAsync(Guid workspaceId, Guid userId);
}
