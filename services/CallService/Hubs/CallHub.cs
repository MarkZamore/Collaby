using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using CallService.Application.Interfaces;
using CallService.Contracts.Requests;

namespace CallService.Hubs;

/// <summary>
/// SignalR-хаб для WebRTC signaling и управления звонками.
///
/// Поддерживает:
/// - аудио- и видеозвонки через P2P WebRTC
/// - демонстрацию экрана
/// - групповые звонки в mesh topology для небольших групп
///
/// Hub отвечает только за signaling: offer -> answer -> ice-candidate.
/// Сами медиапотоки идут напрямую между клиентами и не проходят через сервер.
/// </summary>
[Authorize]
public class CallHub : Hub
{
    private readonly ICallService _callService;
    private readonly ICallParticipantTracker _tracker;
    private readonly ILogger<CallHub> _logger;

    public CallHub(
        ICallService callService,
        ICallParticipantTracker tracker,
        ILogger<CallHub> logger)
    {
        _callService = callService;
        _tracker = tracker;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
                 ?? Context.User?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            _logger.LogInformation("User {UserId} connected to CallHub", userId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            // Если пользователь был в активном звонке, корректно завершаем его участие.
            var activeCallId = await _tracker.GetUserActiveCallAsync(userId);
            if (activeCallId != null)
            {
                var call = await _callService.LeaveCallAsync(activeCallId, userId);

                // Сообщаем остальным участникам, что соединение этого пользователя пропало.
                await Clients.Group($"call:{activeCallId}")
                    .SendAsync("ParticipantLeft", new { userId, callId = activeCallId });

                if (call.Status == "ended")
                {
                    await Clients.Group($"call:{activeCallId}")
                        .SendAsync("CallEnded", call);

                    foreach (var participant in call.Participants.Where(p => p.UserId != userId))
                    {
                        await Clients.Group($"user:{participant.UserId}")
                            .SendAsync("CallEnded", call);
                    }
                }
            }
            _logger.LogInformation("User {UserId} disconnected from CallHub", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Инициатор создаёт звонок и рассылает приглашения всем участникам.
    /// </summary>
    public async Task StartCall(StartCallRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var call = await _callService.StartCallAsync(userId, request);

        // Инициатор сразу входит в группу звонка.
        await Groups.AddToGroupAsync(Context.ConnectionId, $"call:{call.Id}");
        await _tracker.AddToCallAsync(call.Id, userId, Context.ConnectionId);

        // Приглашённые пользователи получают IncomingCall в своей персональной группе.
        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await Clients.Group($"user:{participant.UserId}")
                .SendAsync("IncomingCall", call);
        }

        // Инициатор получает подтверждение создания звонка для синхронизации своего UI.
        await Clients.Caller.SendAsync("CallStarted", call);
    }

    /// <summary>
    /// Участник принимает входящий звонок.
    /// </summary>
    public async Task AnswerCall(string callId, AnswerCallRequest request)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var call = await _callService.AnswerCallAsync(callId, userId, request);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"call:{callId}");
        await _tracker.AddToCallAsync(callId, userId, Context.ConnectionId);

        // Остальные участники сразу видят, что пользователь присоединился.
        await Clients.GroupExcept($"call:{callId}", Context.ConnectionId)
            .SendAsync("ParticipantJoined", new
            {
                userId,
                callId,
                hasVideo = request.HasVideo,
                hasAudio = request.HasAudio
            });

        // Принявший звонок клиент получает актуальный снимок состояния звонка.
        await Clients.Caller.SendAsync("CallAnswered", call);
    }

    /// <summary>
    /// Участник отклоняет входящий звонок.
    /// </summary>
    public async Task DeclineCall(string callId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var call = await _callService.DeclineCallAsync(callId, userId);

        await Clients.Group($"call:{callId}")
            .SendAsync("ParticipantDeclined", new { userId, callId });

        if (call.Status is "declined" or "missed")
        {
            await Clients.Group($"call:{callId}")
                .SendAsync("CallEnded", call);
        }
    }

    /// <summary>
    /// Полностью завершает звонок, обычно по действию инициатора.
    /// </summary>
    public async Task EndCall(string callId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var call = await _callService.EndCallAsync(callId, userId);

        // Участники, уже вошедшие в группу звонка, получают завершающее событие напрямую.
        await Clients.Group($"call:{callId}")
            .SendAsync("CallEnded", call);

        // Дополнительно отправляем событие в персональные группы, чтобы событие получили
        // и пользователи, которые не вступили в call-group (например, входящий ещё "звенит").
        foreach (var participant in call.Participants.Where(p => p.UserId != userId))
        {
            await Clients.Group($"user:{participant.UserId}")
                .SendAsync("CallEnded", call);
        }
    }

    /// <summary>
    /// Участник покидает активный звонок.
    /// </summary>
    public async Task LeaveCall(string callId)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        var call = await _callService.LeaveCallAsync(callId, userId);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"call:{callId}");

        await Clients.Group($"call:{callId}")
            .SendAsync("ParticipantLeft", new { userId, callId });

        if (call.Status == "ended")
        {
            await Clients.Group($"call:{callId}")
                .SendAsync("CallEnded", call);

            // Если звонок завершился, дублируем уведомление в персональные группы.
            foreach (var participant in call.Participants.Where(p => p.UserId != userId))
            {
                await Clients.Group($"user:{participant.UserId}")
                    .SendAsync("CallEnded", call);
            }
        }
    }

    /// <summary>
    /// WebRTC signaling: отправка SDP offer конкретному участнику.
    /// </summary>
    public async Task SendOffer(string callId, Guid targetUserId, object sdpOffer)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await Clients.Group($"user:{targetUserId}")
            .SendAsync("ReceiveOffer", new
            {
                callId,
                fromUserId = userId,
                sdp = sdpOffer
            });
    }

    /// <summary>
    /// WebRTC signaling: отправка SDP answer в ответ на offer.
    /// </summary>
    public async Task SendAnswer(string callId, Guid targetUserId, object sdpAnswer)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await Clients.Group($"user:{targetUserId}")
            .SendAsync("ReceiveAnswer", new
            {
                callId,
                fromUserId = userId,
                sdp = sdpAnswer
            });
    }

    /// <summary>
    /// WebRTC signaling: обмен ICE candidates.
    /// </summary>
    public async Task SendIceCandidate(string callId, Guid targetUserId, object candidate)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await Clients.Group($"user:{targetUserId}")
            .SendAsync("ReceiveIceCandidate", new
            {
                callId,
                fromUserId = userId,
                candidate
            });
    }

    /// <summary>
    /// Переключает видео во время звонка.
    /// </summary>
    public async Task ToggleVideo(string callId, bool enabled)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _callService.ToggleVideoAsync(callId, userId, enabled);

        await Clients.GroupExcept($"call:{callId}", Context.ConnectionId)
            .SendAsync("ParticipantVideoToggled", new { userId, callId, enabled });
    }

    /// <summary>
    /// Переключает аудио во время звонка.
    /// </summary>
    public async Task ToggleAudio(string callId, bool enabled)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _callService.ToggleAudioAsync(callId, userId, enabled);

        await Clients.GroupExcept($"call:{callId}", Context.ConnectionId)
            .SendAsync("ParticipantAudioToggled", new { userId, callId, enabled });
    }

    /// <summary>
    /// Переключает демонстрацию экрана.
    /// Клиент использует navigator.mediaDevices.getDisplayMedia() для захвата экрана,
    /// а затем добавляет video track в уже существующее WebRTC-соединение.
    /// </summary>
    public async Task ToggleScreenShare(string callId, bool enabled)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return;

        await _callService.ToggleScreenShareAsync(callId, userId, enabled);

        await Clients.GroupExcept($"call:{callId}", Context.ConnectionId)
            .SendAsync("ParticipantScreenShareToggled", new { userId, callId, enabled });
    }
}
