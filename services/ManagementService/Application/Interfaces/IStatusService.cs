using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

/// <summary>
/// Интерфейс сервиса для управления статусами проектов
/// </summary>
public interface IStatusService
{
    /// <summary>
    /// Получить все статусы проекта
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Список статусов</returns>
    Task<IEnumerable<ProjectStatus>> GetProjectStatusesAsync(Guid projectId, Guid userId);

    /// <summary>
    /// Создать новый статус в проекте
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="name">Название статуса</param>
    /// <param name="color">Цвет статуса</param>
    /// <param name="isFinal">Является ли статус финальным</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Созданный статус</returns>
    Task<ProjectStatus> CreateStatusAsync(Guid projectId, string name, string? color, bool isFinal, Guid userId);

    /// <summary>
    /// Обновить статус
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <param name="name">Новое название</param>
    /// <param name="color">Новый цвет</param>
    /// <param name="isFinal">Является ли статус финальным</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Обновленный статус или null</returns>
    Task<ProjectStatus?> UpdateStatusAsync(Guid statusId, string name, string? color, bool isFinal, Guid userId);

    /// <summary>
    /// Удалить статус
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>true если удален успешно</returns>
    Task<bool> DeleteStatusAsync(Guid statusId, Guid userId);

    /// <summary>
    /// Изменить порядок статусов проекта
    /// </summary>
    /// <param name="projectId">ID проекта</param>
    /// <param name="statusIds">Массив ID статусов в новом порядке</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>true если порядок изменен успешно</returns>
    Task<bool> ReorderStatusesAsync(Guid projectId, Guid[] statusIds, Guid userId);

    /// <summary>
    /// Получить статус по ID
    /// </summary>
    /// <param name="statusId">ID статуса</param>
    /// <param name="userId">ID пользователя</param>
    /// <returns>Статус или null</returns>
    Task<ProjectStatus?> GetStatusByIdAsync(Guid statusId, Guid userId);
}