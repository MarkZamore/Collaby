namespace CallService.Contracts.Requests;

public class StartCallRequest
{
    public string? ChatId { get; set; }
    public string Type { get; set; } = "audio"; // Допустимые типы: audio, video.
    public List<Guid> ParticipantIds { get; set; } = new();
}

public class AnswerCallRequest
{
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; } = true;
}
