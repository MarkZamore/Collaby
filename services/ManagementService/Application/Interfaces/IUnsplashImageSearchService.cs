namespace ManagementService.Application.Interfaces;

/// <summary>
/// Поиск изображений через Unsplash API для фона доски.
/// </summary>
public interface IUnsplashImageSearchService
{
    /// <summary>
    /// Ищет фото по запросу (страница page, 1-based). Возвращает пустой список, если ключ не настроен или произошла ошибка.
    /// </summary>
    Task<IReadOnlyList<UnsplashImageResult>> SearchAsync(string query, int perPage = 12, int page = 1, CancellationToken cancellationToken = default);
}

public record UnsplashImageResult(string Id, string Url);
