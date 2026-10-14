namespace CallService.Contracts.Requests;

/// <summary>
/// Сообщения WebRTC signaling, которые передаются через SignalR.
/// </summary>
public class SignalMessage
{
    public string CallId { get; set; } = null!;
    public Guid TargetUserId { get; set; }
    public string Type { get; set; } = null!; // Допустимые типы: offer, answer, ice-candidate.
    public object Payload { get; set; } = null!;
}
