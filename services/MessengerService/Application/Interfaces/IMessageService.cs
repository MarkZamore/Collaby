using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;

namespace MessengerService.Application.Interfaces;

public interface IMessageService
{
    Task<MessageResponse> SendMessageAsync(string chatId, Guid senderId, SendMessageRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MessageResponse>> GetMessagesAsync(string chatId, Guid userId, int limit = 50, string? before = null, CancellationToken ct = default);
    Task<MessageResponse> UpdateMessageAsync(string messageId, Guid userId, UpdateMessageRequest request, CancellationToken ct = default);
    Task DeleteMessageAsync(string messageId, Guid userId, CancellationToken ct = default);
    Task<MessageResponse> ToggleReactionAsync(string messageId, Guid userId, ToggleReactionRequest request, CancellationToken ct = default);
}
