using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

/// <summary>
/// Сервис для работы с приглашениями
/// </summary>
public interface IInvitationService
{
    /// <summary>
    /// Создать приглашение
    /// </summary>
    Task<WorkspaceInvitation> CreateInvitationAsync(Guid workspaceId, string email, string role, int expiresInDays, Guid invitedBy);

    /// <summary>
    /// Принять приглашение
    /// </summary>
    Task<WorkspaceMember> AcceptInvitationAsync(string token, Guid userId);

    /// <summary>
    /// Отозвать приглашение
    /// </summary>
    Task<bool> RevokeInvitationAsync(Guid invitationId, Guid userId);

    /// <summary>
    /// Получить приглашения workspace
    /// </summary>
    Task<IEnumerable<WorkspaceInvitation>> GetWorkspaceInvitationsAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Получить приглашение по токену
    /// </summary>
    Task<WorkspaceInvitation?> GetInvitationByTokenAsync(string token);

    /// <summary>
    /// Проверяет, закрывает ли текущий пользователь lifecycle приглашения своим уже активным membership.
    /// </summary>
    Task<bool> IsInvitationAlreadySatisfiedByUserAsync(Guid workspaceId, string invitationEmail, Guid userId);
}
