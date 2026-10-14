using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MessengerService.Domain.Entities;

public class Chat
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("type")]
    public string Type { get; set; } = null!; // private, project, channel

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("projectId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? ProjectId { get; set; }

    [BsonElement("workspaceId")]
    [BsonRepresentation(BsonType.String)]
    public Guid? WorkspaceId { get; set; }

    [BsonElement("createdBy")]
    [BsonRepresentation(BsonType.String)]
    public Guid CreatedBy { get; set; }

    [BsonElement("members")]
    public List<ChatMember> Members { get; set; } = new();

    [BsonElement("lastMessage")]
    public LastMessageInfo? LastMessage { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMember
{
    [BsonElement("userId")]
    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = "member"; // admin, member

    [BsonElement("joinedAt")]
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastReadAt")]
    public DateTime? LastReadAt { get; set; }
}

public class LastMessageInfo
{
    [BsonElement("messageId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? MessageId { get; set; }

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("senderId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderId { get; set; }

    [BsonElement("sentAt")]
    public DateTime SentAt { get; set; }
}
