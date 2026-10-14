namespace CallService.Application.Interfaces;

public interface ICallParticipantTracker
{
    Task AddToCallAsync(string callId, Guid userId, string connectionId);
    Task RemoveFromCallAsync(string callId, Guid userId);
    Task<IReadOnlyList<Guid>> GetActiveParticipantsAsync(string callId);
    Task<string?> GetUserConnectionAsync(string callId, Guid userId);
    Task<string?> GetUserActiveCallAsync(Guid userId);
    Task ClearCallAsync(string callId);
}
