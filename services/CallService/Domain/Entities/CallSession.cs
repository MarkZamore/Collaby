using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CallService.Domain.Entities;

public class CallSession
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("chatId")]
    public string? ChatId { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = "audio"; // Допустимые типы: audio, video.

    [BsonElement("status")]
    public string Status { get; set; } = "ringing"; // Возможные статусы: ringing, active, ended, missed, declined.

    [BsonElement("initiatorId")]
    [BsonRepresentation(BsonType.String)]
    public Guid InitiatorId { get; set; }

    [BsonElement("isGroup")]
    public bool IsGroup { get; set; }

    [BsonElement("participants")]
    public List<CallParticipant> Participants { get; set; } = new();

    [BsonElement("startedAt")]
    public DateTime? StartedAt { get; set; }

    [BsonElement("endedAt")]
    public DateTime? EndedAt { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CallParticipant
{
    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "ringing"; // Возможные статусы: ringing, joined, left, declined.

    [BsonElement("joinedAt")]
    public DateTime? JoinedAt { get; set; }

    [BsonElement("leftAt")]
    public DateTime? LeftAt { get; set; }

    [BsonElement("hasVideo")]
    public bool HasVideo { get; set; }

    [BsonElement("hasAudio")]
    public bool HasAudio { get; set; } = true;

    [BsonElement("isScreenSharing")]
    public bool IsScreenSharing { get; set; }
}
