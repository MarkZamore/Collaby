using ManagementService.Contracts.Responses;
using ManagementService.Domain.Enums;

namespace ManagementService.Application.Interfaces;

public interface INotificationService
{
    /// <summary>Создать и отправить уведомление (сохранить в БД + push через SignalR)</summary>
    Task<NotificationResponse> CreateAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid? referenceId = null,
        Guid? secondaryReferenceId = null,
        Guid? actorId = null,
        string? actionStatus = null);

    /// <summary>Получить уведомления пользователя (новые сверху)</summary>
    Task<IEnumerable<NotificationResponse>> GetUserNotificationsAsync(Guid userId, int limit = 50, int offset = 0);

    /// <summary>Количество непрочитанных</summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>Пометить одно уведомление прочитанным</summary>
    Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>Пометить все прочитанными</summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>Обновить actionStatus (для инвайтов: accepted/declined)</summary>
    Task<bool> SetActionStatusAsync(Guid notificationId, Guid userId, string actionStatus);
}
