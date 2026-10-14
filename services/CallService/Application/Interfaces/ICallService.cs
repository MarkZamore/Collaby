using CallService.Contracts.Requests;
using CallService.Contracts.Responses;

namespace CallService.Application.Interfaces;

public interface ICallService
{
    Task<CallSessionResponse> StartCallAsync(Guid initiatorId, StartCallRequest request, CancellationToken ct = default);
    Task<CallSessionResponse> AnswerCallAsync(string callId, Guid userId, AnswerCallRequest request, CancellationToken ct = default);
    Task<CallSessionResponse> DeclineCallAsync(string callId, Guid userId, CancellationToken ct = default);
    Task<CallSessionResponse> EndCallAsync(string callId, Guid userId, CancellationToken ct = default);
    Task<CallSessionResponse> LeaveCallAsync(string callId, Guid userId, CancellationToken ct = default);
    Task<CallSessionResponse?> GetCallAsync(string callId, CancellationToken ct = default);
    Task<CallSessionResponse?> GetActiveCallAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CallSessionResponse>> GetCallHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default);
    Task ToggleVideoAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default);
    Task ToggleAudioAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default);
    Task ToggleScreenShareAsync(string callId, Guid userId, bool enabled, CancellationToken ct = default);
}
