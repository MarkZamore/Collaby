namespace MessengerService.Application.Interfaces;

public interface IUserLookupClient
{
    Task<IReadOnlyDictionary<Guid, UserLookupResult>> GetUsersByIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

public class UserLookupResult
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
