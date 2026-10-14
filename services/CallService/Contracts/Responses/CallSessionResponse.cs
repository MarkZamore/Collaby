namespace CallService.Contracts.Responses;

public class CallSessionResponse
{
    public string Id { get; set; } = null!;
    public string? ChatId { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid InitiatorId { get; set; }
    public bool IsGroup { get; set; }
    public List<CallParticipantResponse> Participants { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CallParticipantResponse
{
    public Guid UserId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public bool HasVideo { get; set; }
    public bool HasAudio { get; set; }
    public bool IsScreenSharing { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
}
