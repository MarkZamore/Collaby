namespace ManagementService.Application.Interfaces;

/// <summary>
/// Представляет результат поиска пользовательского профиля в UserService.
/// </summary>
public sealed class UserLookupResult
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// Клиент для внутренних service-to-service вызовов в UserService.
/// </summary>
public interface IUserLookupClient
{
    /// <summary>
    /// Получает profile- и identity-информацию для набора user ID.
    /// Пользователи, которых не удалось найти, исключаются из результата.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет пользователей по username или display name.
    /// </summary>
    Task<IReadOnlyList<UserLookupResult>> SearchUsersAsync(
        string query,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
