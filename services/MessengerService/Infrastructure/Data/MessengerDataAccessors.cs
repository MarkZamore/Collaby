using MongoDB.Driver;
using StackExchange.Redis;

namespace MessengerService.Infrastructure.Data;

public sealed class MessengerMongoDatabaseAccessor
{
    public MessengerMongoDatabaseAccessor(IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoDatabase Database { get; }
}

public sealed class MessengerRedisConnectionAccessor
{
    public MessengerRedisConnectionAccessor(IConnectionMultiplexer connection)
    {
        Connection = connection;
    }

    public IConnectionMultiplexer Connection { get; }
}
