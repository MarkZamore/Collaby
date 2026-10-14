using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MessengerService.Domain.Entities;

public class Message
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("chatId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ChatId { get; set; } = null!;

    [BsonElement("senderId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderId { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = "text"; // text, file, system

    [BsonElement("text")]
    public string? Text { get; set; }

    [BsonElement("file")]
    public FileAttachment? File { get; set; }

    [BsonElement("replyTo")]
    public ReplyInfo? ReplyTo { get; set; }

    [BsonElement("reactions")]
    public List<Reaction> Reactions { get; set; } = new();

    [BsonElement("isEdited")]
    public bool IsEdited { get; set; }

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FileAttachment
{
    [BsonElement("fileName")]
    public string FileName { get; set; } = null!;

    [BsonElement("fileUrl")]
    public string FileUrl { get; set; } = null!;

    [BsonElement("fileSize")]
    public long FileSize { get; set; }

    [BsonElement("contentType")]
    public string ContentType { get; set; } = null!;
}

public class ReplyInfo
{
    [BsonElement("messageId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string MessageId { get; set; } = null!;

    [BsonElement("senderId")]
    [BsonRepresentation(BsonType.String)]
    public Guid SenderId { get; set; }

    [BsonElement("text")]
    public string? Text { get; set; }
}

public class Reaction
{
    [BsonElement("emoji")]
    public string Emoji { get; set; } = null!;

    [BsonElement("userIds")]
    public List<Guid> UserIds { get; set; } = new();
}
