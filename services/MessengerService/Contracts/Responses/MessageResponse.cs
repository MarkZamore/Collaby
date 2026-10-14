namespace MessengerService.Contracts.Responses;

public class MessageResponse
{
    public string Id { get; set; } = null!;
    public string ChatId { get; set; } = null!;
    public Guid SenderId { get; set; }
    public string Type { get; set; } = null!;
    public string? Text { get; set; }
    public FileAttachmentResponse? File { get; set; }
    public ReplyInfoResponse? ReplyTo { get; set; }
    public List<ReactionResponse> Reactions { get; set; } = new();
    public bool IsEdited { get; set; }
    public bool IsDeleted { get; set; }
    public string? SenderUsername { get; set; }
    public string? SenderDisplayName { get; set; }
    public string? SenderAvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class FileAttachmentResponse
{
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = null!;
}

public class ReplyInfoResponse
{
    public string MessageId { get; set; } = null!;
    public Guid SenderId { get; set; }
    public string? Text { get; set; }
}

public class ReactionResponse
{
    public string Emoji { get; set; } = null!;
    public List<Guid> UserIds { get; set; } = new();
    public int Count { get; set; }
}
