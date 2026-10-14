namespace ManagementService.Contracts.Responses;

/// <summary>
/// Ответ с информацией о рабочем пространстве
/// </summary>
public class WorkspaceResponse
{
    /// <summary>
    /// Уникальный идентификатор
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название рабочего пространства
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// ID владельца
    /// </summary>
    public Guid OwnerId { get; set; }

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Архивировано ли
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Список членов рабочего пространства
    /// </summary>
    public List<WorkspaceMemberResponse> Members { get; set; } = new();
}
