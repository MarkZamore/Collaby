namespace MessengerService.Contracts.Responses;

public class ChatResponse
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid CreatedBy { get; set; }
    public List<ChatMemberResponse> Members { get; set; } = new();
    public LastMessageResponse? LastMessage { get; set; }
    public int UnreadCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatMemberResponse
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = null!;
    public DateTime JoinedAt { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}

public class LastMessageResponse
{
    public string? MessageId { get; set; }
    public string? Text { get; set; }
    public Guid SenderId { get; set; }
    public DateTime SentAt { get; set; }
}
