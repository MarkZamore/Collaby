namespace GamificationService.Application.Interfaces;

public sealed class UserLookupResult
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
}

public interface IUserLookupClient
{
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
