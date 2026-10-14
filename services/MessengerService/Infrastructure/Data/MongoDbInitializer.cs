using MongoDB.Driver;
using MessengerService.Domain.Entities;

namespace MessengerService.Infrastructure.Data;

public static class MongoDbInitializer
{
    public static async Task InitializeAsync(IMongoDatabase database)
    {
        // Индексы для коллекции сообщений.
        var messages = database.GetCollection<Message>("messages");
        await messages.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Message>(
                Builders<Message>.IndexKeys.Ascending(m => m.ChatId).Descending(m => m.CreatedAt)),
            new CreateIndexModel<Message>(
                Builders<Message>.IndexKeys.Ascending(m => m.SenderId)),
        });

        // Индексы для коллекции чатов.
        var chats = database.GetCollection<Chat>("chats");
        await chats.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Chat>(
                Builders<Chat>.IndexKeys.Ascending("members.userId")),
            new CreateIndexModel<Chat>(
                Builders<Chat>.IndexKeys.Ascending(c => c.ProjectId)),
            new CreateIndexModel<Chat>(
                Builders<Chat>.IndexKeys.Ascending(c => c.WorkspaceId)),
            new CreateIndexModel<Chat>(
                Builders<Chat>.IndexKeys.Descending(c => c.UpdatedAt)),
        });
    }
}
