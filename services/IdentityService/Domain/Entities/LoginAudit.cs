using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace IdentityService.Domain.Entities;

[BsonIgnoreExtraElements]
public class LoginAudit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    
    [BsonElement("userId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? UserId { get; set; }
    
    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;
    
    [BsonElement("action")]
    public string Action { get; set; } = string.Empty; // Возможные значения: login, register, logout, refresh.
    
    [BsonElement("success")]
    public bool Success { get; set; }
    
    [BsonElement("ipAddress")]
    public string? IpAddress { get; set; }
    
    [BsonElement("userAgent")]
    public string? UserAgent { get; set; }
    
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }
    
    [BsonElement("timestamp")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [BsonElement("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
