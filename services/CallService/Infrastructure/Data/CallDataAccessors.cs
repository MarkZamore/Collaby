using MongoDB.Driver;
using StackExchange.Redis;

namespace CallService.Infrastructure.Data;

public sealed class CallMongoDatabaseAccessor
{
    public CallMongoDatabaseAccessor(IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoDatabase Database { get; }
}

public sealed class CallRedisConnectionAccessor
{
    public CallRedisConnectionAccessor(IConnectionMultiplexer connection)
    {
        Connection = connection;
    }

    public IConnectionMultiplexer Connection { get; }
}
