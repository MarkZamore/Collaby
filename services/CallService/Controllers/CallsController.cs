using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using CallService.Application.Interfaces;
using CallService.Contracts.Requests;
using CallService.Contracts.Responses;
using CallService.Hubs;

namespace CallService.Controllers;

[ApiController]
[Route("")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class CallsController : ControllerBase
{
    private readonly ICallService _callService;
    private readonly IHubContext<CallHub> _hubContext;

    public CallsController(ICallService callService, IHubContext<CallHub> hubContext)
    {
        _callService = callService;
        _hubContext = hubContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CallSessionResponse>> StartCall([FromBody] StartCallRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.StartCallAsync(userId, request, ct);

        // Рассылаем приглашение всем участникам через SignalR.
        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await _hubContext.Clients.Group($"user:{participant.UserId}")
                .SendAsync("IncomingCall", call, ct);
        }

        // Инициатор тоже получает событие, чтобы его UI сразу показал состояние CallStarted.
        await _hubContext.Clients.Group($"user:{userId}")
            .SendAsync("CallStarted", call, ct);

        return CreatedAtAction(nameof(GetCall), new { callId = call.Id }, call);
    }

    [HttpPost("{callId}/answer")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> AnswerCall(string callId, [FromBody] AnswerCallRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.AnswerCallAsync(callId, userId, request, ct);

        // Остальные участники получают уведомление о подключении пользователя.
        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await _hubContext.Clients.Group($"user:{participant.UserId}")
                .SendAsync("ParticipantJoined", new
                {
                    userId,
                    callId,
                    hasVideo = request.HasVideo,
                    hasAudio = request.HasAudio
                }, ct);
        }

        return Ok(call);
    }

    [HttpPost("{callId}/decline")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> DeclineCall(string callId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.DeclineCallAsync(callId, userId, ct);

        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await _hubContext.Clients.Group($"user:{participant.UserId}")
                .SendAsync("ParticipantDeclined", new { userId, callId }, ct);
        }

        if (call.Status is "declined" or "missed")
        {
            foreach (var participant in call.Participants.Where(p => p.UserId != userId))
            {
                await _hubContext.Clients.Group($"user:{participant.UserId}")
                    .SendAsync("CallEnded", call, ct);
            }
        }

        return Ok(call);
    }

    [HttpPost("{callId}/end")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> EndCall(string callId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.EndCallAsync(callId, userId, ct);

        await _hubContext.Clients.Group($"call:{callId}")
            .SendAsync("CallEnded", call, ct);

        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await _hubContext.Clients.Group($"user:{participant.UserId}")
                .SendAsync("CallEnded", call, ct);
        }

        return Ok(call);
    }

    [HttpPost("{callId}/leave")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> LeaveCall(string callId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.LeaveCallAsync(callId, userId, ct);

        await _hubContext.Clients.Group($"call:{callId}")
            .SendAsync("ParticipantLeft", new { userId, callId }, ct);

        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await _hubContext.Clients.Group($"user:{participant.UserId}")
                .SendAsync("ParticipantLeft", new { userId, callId }, ct);
        }

        if (call.Status == "ended")
        {
            await _hubContext.Clients.Group($"call:{callId}")
                .SendAsync("CallEnded", call, ct);

            foreach (var participant in call.Participants.Where(p => p.UserId != userId))
            {
                await _hubContext.Clients.Group($"user:{participant.UserId}")
                    .SendAsync("CallEnded", call, ct);
            }
        }

        return Ok(call);
    }

    [HttpGet("{callId:length(24)}")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> GetCall(string callId, CancellationToken ct)
    {
        var call = await _callService.GetCallAsync(callId, ct);
        if (call == null) return NotFound();
        return Ok(call);
    }

    [HttpGet("active")]
    [ProducesResponseType(typeof(CallSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CallSessionResponse>> GetActiveCall(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var call = await _callService.GetActiveCallAsync(userId, ct);
        if (call == null) return NotFound();
        return Ok(call);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<CallSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CallSessionResponse>>> GetCallHistory(
        [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var history = await _callService.GetCallHistoryAsync(userId, limit, ct);
        return Ok(history);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
            throw new UnauthorizedAccessException("Unable to extract user ID from token");
        return userId;
    }
}
