using MessengerService.Contracts.Requests;
using MessengerService.Contracts.Responses;

namespace MessengerService.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponse> CreatePrivateChatAsync(Guid userId, CreatePrivateChatRequest request, CancellationToken ct = default);
    Task<ChatResponse> CreateProjectChatAsync(Guid userId, CreateProjectChatRequest request, CancellationToken ct = default);
    Task<ChatResponse> CreateChannelAsync(Guid userId, CreateChannelRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ChatResponse>> GetUserChatsAsync(Guid userId, CancellationToken ct = default);
    Task<ChatResponse?> GetChatByIdAsync(string chatId, Guid userId, CancellationToken ct = default);
    Task<DeleteChatResult> DeleteChatAsync(string chatId, Guid userId, CancellationToken ct = default);
    Task<ClearChatResult> ClearChatAsync(string chatId, Guid userId, CancellationToken ct = default);
    Task<ChatResponse> UpdateChatAsync(string chatId, Guid userId, UpdateChatRequest request, CancellationToken ct = default);
    Task AddMembersAsync(string chatId, Guid userId, AddChatMembersRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(string chatId, Guid userId, Guid memberUserId, CancellationToken ct = default);
    Task LeaveChatAsync(string chatId, Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(string chatId, Guid userId, CancellationToken ct = default);
}
