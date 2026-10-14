using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

/// <summary>
/// Интерфейс сервиса для управления проектами
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Создать новый проект в workspace и автоматически создать дефолтные статусы
    /// </summary>
    /// <param name="workspaceId">ID рабочего пространства</param>
    /// <param name="name">Название проекта</param>
    /// <param name="description">Описание проекта</param>
    /// <param name="userId">ID пользователя-создателя</param>
    /// <returns>Созданный проект со статусами</returns>
    Task<Project> CreateProjectAsync(Guid workspaceId, string name, string? description, Guid userId);

    /// <summary>
    /// Создать дефолтные метки для проекта (если их ещё нет)
    /// </summary>
    System.Threading.Tasks.Task EnsureDefaultLabelsAsync(Guid projectId);

    /// <summary>
    /// Получить все проекты workspace
    /// </summary>
    /// <param name="workspaceId">ID рабочего пространства</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Список проектов</returns>
    Task<IEnumerable<Project>> GetProjectsByWorkspaceAsync(Guid workspaceId, Guid userId);

    /// <summary>
    /// Получить проект по ID
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Проект или null</returns>
    Task<Project?> GetProjectByIdAsync(Guid projectId, Guid userId);

    /// <summary>
    /// Проверить доступ пользователя к проекту через workspace membership
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>true если есть доступ</returns>
    Task<bool> HasAccessToProjectAsync(Guid projectId, Guid userId);

    /// <summary>
    /// Обновить список стикеров доски
    /// </summary>
    Task<Project?> UpdateBoardStickerIdsAsync(Guid projectId, List<string> stickerIds, Guid userId);

    /// <summary>
    /// Удалить проект (только владелец или администратор workspace)
    /// </summary>
    Task<bool> DeleteProjectAsync(Guid projectId, Guid userId);
}