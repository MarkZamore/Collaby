using ManagementService.Contracts.Responses;

namespace ManagementService.Application.Interfaces;

/// <summary>
/// Сервис для агрегации данных при начальной загрузке приложения.
/// </summary>
public interface IBootstrapService
{
    /// <summary>
    /// Собрать все данные, необходимые фронтенду при старте.
    /// </summary>
    /// <param name="userId">ID текущего пользователя (из JWT)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Агрегированный BootstrapResponse</returns>
    Task<BootstrapResponse> GetBootstrapDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
