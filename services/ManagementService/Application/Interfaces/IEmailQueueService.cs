using ManagementService.Application.Events;

namespace ManagementService.Application.Interfaces;

/// <summary>
/// Сервис для работы с email queue
/// </summary>
public interface IEmailQueueService
{
    /// <summary>
    /// Добавить email приглашения в очередь
    /// </summary>
    Task QueueInvitationEmailAsync(InvitationEmailEvent emailEvent);
}
