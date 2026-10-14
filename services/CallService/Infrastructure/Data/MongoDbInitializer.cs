using MongoDB.Driver;
using CallService.Domain.Entities;

namespace CallService.Infrastructure.Data;

public static class MongoDbInitializer
{
    public static async Task InitializeAsync(IMongoDatabase database)
    {
        var calls = database.GetCollection<CallSession>("callSessions");
        await calls.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CallSession>(
                Builders<CallSession>.IndexKeys.Ascending("participants.userId")),
            new CreateIndexModel<CallSession>(
                Builders<CallSession>.IndexKeys.Ascending(c => c.Status)),
            new CreateIndexModel<CallSession>(
                Builders<CallSession>.IndexKeys.Descending(c => c.CreatedAt)),
            new CreateIndexModel<CallSession>(
                Builders<CallSession>.IndexKeys.Ascending(c => c.ChatId)),
        });
    }
}
