using MessengerService.Contracts.Responses;

namespace MessengerService.Application.Interfaces;

public interface IPresenceService
{
    Task SetOnlineAsync(Guid userId);
    Task SetOfflineAsync(Guid userId);
    Task<bool> IsOnlineAsync(Guid userId);
    Task<IReadOnlyList<PresenceResponse>> GetPresenceAsync(IEnumerable<Guid> userIds);
    Task SetTypingAsync(Guid userId, string chatId);
    Task ClearTypingAsync(Guid userId, string chatId);
    Task<IReadOnlyList<Guid>> GetTypingUsersAsync(string chatId);
}
