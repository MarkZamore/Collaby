using MessengerService.Domain.Entities;
using MongoDB.Driver;

namespace MessengerService.Application.Services;

internal interface IChatDeletionStore
{
    Task<Chat?> GetChatAsync(string chatId, CancellationToken ct = default);
    Task DeleteChatAsync(string chatId, CancellationToken ct = default);
    Task DeleteMessagesAsync(string chatId, CancellationToken ct = default);
}

internal sealed class MongoChatDeletionStore : IChatDeletionStore
{
    private readonly IMongoCollection<Chat> _chats;
    private readonly IMongoCollection<Message> _messages;

    public MongoChatDeletionStore(IMongoCollection<Chat> chats, IMongoCollection<Message> messages)
    {
        _chats = chats;
        _messages = messages;
    }

    public async Task<Chat?> GetChatAsync(string chatId, CancellationToken ct = default)
    {
        return await _chats.Find(chat => chat.Id == chatId).FirstOrDefaultAsync(ct);
    }

    public async Task DeleteChatAsync(string chatId, CancellationToken ct = default)
    {
        await _chats.DeleteOneAsync(chat => chat.Id == chatId, ct);
    }

    public async Task DeleteMessagesAsync(string chatId, CancellationToken ct = default)
    {
        await _messages.DeleteManyAsync(message => message.ChatId == chatId, ct);
    }
}
