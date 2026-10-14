namespace ManagementService.Contracts.Responses;

/// <summary>
/// Ответ с информацией о члене рабочего пространства
/// </summary>
public class WorkspaceMemberResponse
{
    /// <summary>
    /// ID члена
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID пользователя
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Роль в рабочем пространстве (Owner, Member, Guest)
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Статус (Active, Pending, Inactive)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Дата присоединения
    /// </summary>
    public DateTime JoinedAt { get; set; }

    /// <summary>
    /// ID workspace
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// Активен ли участник
    /// </summary>
    public bool IsActive { get; set; }

    // ── Profile fields enriched from UserService ─────────────────────────────

    /// <summary>
    /// Имя пользователя (логин)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Отображаемое имя пользователя
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// URL аватара пользователя
    /// </summary>
    public string? AvatarUrl { get; set; }
}
