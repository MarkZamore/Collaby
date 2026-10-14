using IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace IdentityService.Infrastructure.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("Mongo") ?? configuration["ConnectionStrings:Mongo"];
        if (string.IsNullOrEmpty(conn))
            throw new InvalidOperationException("MongoDB connection string is not configured");

        var client = new MongoClient(conn);
        var dbName = configuration.GetSection("Mongo:Database").Value ?? "identity_logs";
        _database = client.GetDatabase(dbName);
    }

    public IMongoDatabase Database => _database;

    public IMongoCollection<LoginAudit> LoginAudits => _database.GetCollection<LoginAudit>("login_audits");
}
