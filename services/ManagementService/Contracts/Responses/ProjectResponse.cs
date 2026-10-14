namespace ManagementService.Contracts.Responses;

/// <summary>
/// Ответ с данными о проекте
/// </summary>
public class ProjectResponse
{
    /// <summary>
    /// ID проекта
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название проекта
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание проекта
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// ID рабочего пространства
    /// </summary>
    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// ID создателя проекта
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Дата создания
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Архивирован ли проект
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// ID стикеров, включённых на доске
    /// </summary>
    public List<string>? BoardStickerIds { get; set; }

    /// <summary>
    /// Статусы проекта
    /// </summary>
    public ICollection<ProjectStatusResponse> Statuses { get; set; } = new List<ProjectStatusResponse>();
}

/// <summary>
/// Ответ с данными о статусе проекта
/// </summary>
public class ProjectStatusResponse
{
    /// <summary>
    /// ID статуса
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название статуса
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Порядок в списке
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Цвет статуса
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Является ли финальным статусом
    /// </summary>
    public bool IsFinal { get; set; }
}