namespace MessengerService.Contracts.Responses;

public class PresenceResponse
{
    public Guid UserId { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
