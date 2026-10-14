namespace ManagementService.Contracts.Responses;

/// <summary>
/// Ответ с данными приглашения
/// </summary>
public class InvitationResponse
{
    /// <summary>
    /// ID приглашения
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
    /// Название рабочего пространства
    /// </summary>
    public string? WorkspaceName { get; set; }

    /// <summary>
    /// Роль
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Токен приглашения (только при создании)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Кто создал приглашение
    /// </summary>
    public Guid InvitedBy { get; set; }

    /// <summary>
    /// Когда было создано
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Когда истекает
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Когда было принято
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Кто принял
    /// </summary>
    public Guid? AcceptedBy { get; set; }

    /// <summary>
    /// Статус (Pending, Accepted, Expired, Revoked)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Признак того, что текущий viewer уже состоит в workspace по этому приглашению.
    /// </summary>
    public bool AlreadyMember { get; set; }

    /// <summary>
    /// Может ли viewer продолжить lifecycle принятия из текущего состояния preview.
    /// </summary>
    public bool CanAccept { get; set; }
}
