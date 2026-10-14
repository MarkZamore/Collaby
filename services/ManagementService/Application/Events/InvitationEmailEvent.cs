namespace ManagementService.Application.Events;

/// <summary>
/// Событие отправки email приглашения
/// </summary>
public class InvitationEmailEvent
{
    /// <summary>
    /// Email получателя
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Название workspace
    /// </summary>
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>
    /// Имя отправителя
    /// </summary>
    public string InviterName { get; set; } = string.Empty;

    /// <summary>
    /// Токен приглашения
    /// </summary>
    public string InvitationToken { get; set; } = string.Empty;

    /// <summary>
    /// Роль
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Срок действия
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}
