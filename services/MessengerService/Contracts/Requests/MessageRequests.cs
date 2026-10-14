namespace MessengerService.Contracts.Requests;

public class SendMessageRequest
{
    public string? Text { get; set; }
    public string Type { get; set; } = "text"; // Допустимые типы: text, file, system.
    public string? ReplyToMessageId { get; set; }
    public FileAttachmentRequest? File { get; set; }
}

public class FileAttachmentRequest
{
    public string FileName { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = null!;
}

public class UpdateMessageRequest
{
    public string Text { get; set; } = null!;
}

public class ToggleReactionRequest
{
    public string Emoji { get; set; } = null!;
}
