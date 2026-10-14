using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GamificationService.Infrastructure.Documents;

public sealed class GamificationEventDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public Guid SourceEventId { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public Guid UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? TaskId { get; set; }
    public string? Priority { get; set; }
    public string? Metadata { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? RewardJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
