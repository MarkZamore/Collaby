using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;
using ManagementService.Domain.Entities;
using ManagementService.Domain.Enums;
using ManagementService.Hubs;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

public class NotificationService : INotificationService
{
    private static readonly EventId NotificationPushFailed = new(7101, nameof(NotificationPushFailed));
    private static readonly EventId NotificationUnreadCountPushFailed = new(7102, nameof(NotificationUnreadCountPushFailed));

    private readonly ManagementDbContext _context;
    private readonly IHubContext<WorkspaceHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ManagementDbContext context,
        IHubContext<WorkspaceHub> hub,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hub = hub;
        _logger = logger;
    }

    public async Task<NotificationResponse> CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? referenceId = null,
        Guid? secondaryReferenceId = null,
        Guid? actorId = null,
        string? actionStatus = null)
    {
        if (type == NotificationType.WorkspaceInvite && referenceId.HasValue)
        {
            var existingPendingInvite = await _context.Notifications
                .Where(n => n.UserId == userId
                    && n.Type == NotificationType.WorkspaceInvite
                    && n.ReferenceId == referenceId
                    && string.IsNullOrEmpty(n.ActionStatus))
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingPendingInvite != null)
            {
                _logger.LogInformation(
                    "Duplicate workspace invite notification skipped. UserId={UserId} WorkspaceId={WorkspaceId} NotificationId={NotificationId}",
                    userId,
                    referenceId,
                    existingPendingInvite.Id);
                return MapToResponse(existingPendingInvite);
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ReferenceId = referenceId,
            SecondaryReferenceId = secondaryReferenceId,
            ActorId = actorId,
            ActionStatus = actionStatus,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        var response = MapToResponse(notification);

        // Отправляем уведомление пользователю через SignalR-группу вида user:{userId}.
        try
        {
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("NotificationReceived", response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                NotificationPushFailed,
                ex,
                "Notification delivery degraded after persistence. NotificationId={NotificationId} UserId={UserId}",
                notification.Id,
                userId);
        }

        _logger.LogInformation("Notification {Type} created for user {UserId}: {Title}", type, userId, title);
        return response;
    }

    public async Task<IEnumerable<NotificationResponse>> GetUserNotificationsAsync(Guid userId, int limit = 50, int offset = 0)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return notifications.Select(MapToResponse);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification == null) return false;

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        // Отдельно отправляем обновлённый счётчик непрочитанных уведомлений.
        await PushUnreadCount(userId);
        return true;
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));

        await PushUnreadCount(userId);
    }

    public async Task<bool> SetActionStatusAsync(Guid notificationId, Guid userId, string actionStatus)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notification == null) return false;

        notification.ActionStatus = actionStatus;
        notification.IsRead = true;
        await _context.SaveChangesAsync();

        await PushUnreadCount(userId);
        return true;
    }

    private async Task PushUnreadCount(Guid userId)
    {
        try
        {
            var count = await GetUnreadCountAsync(userId);
            await _hub.Clients.Group($"user:{userId}")
                .SendAsync("UnreadCountChanged", new { count });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                NotificationUnreadCountPushFailed,
                ex,
                "Unread count broadcast degraded after notification state change. UserId={UserId}",
                userId);
        }
    }

    private static NotificationResponse MapToResponse(Notification n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        Type = n.Type.ToString(),
        Title = n.Title,
        Message = n.Message,
        ReferenceId = n.ReferenceId,
        SecondaryReferenceId = n.SecondaryReferenceId,
        ActorId = n.ActorId,
        IsRead = n.IsRead,
        ActionStatus = n.ActionStatus,
        CreatedAt = n.CreatedAt,
    };
}
