namespace ManagementService.Domain.Entities;

/// <summary>
/// Приглашение в рабочее пространство
/// </summary>
public class WorkspaceInvitation
{
    /// <summary>
    /// Уникальный идентификатор приглашения
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID рабочего пространства
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Email приглашенного пользователя
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Роль, которая будет назначена после принятия приглашения
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Уникальный токен приглашения
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Кто создал приглашение
    /// </summary>
    public Guid InvitedBy { get; set; }

    /// <summary>
    /// Когда было создано приглашение
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Когда истекает приглашение
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Когда приглашение было принято (null если еще не принято)
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// ID пользователя, который принял приглашение
    /// </summary>
    public Guid? AcceptedBy { get; set; }

    /// <summary>
    /// Статус приглашения (Pending, Accepted, Expired, Revoked)
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Навигационное свойство к рабочему пространству
    /// </summary>
    public Workspace? Workspace { get; set; }
}
