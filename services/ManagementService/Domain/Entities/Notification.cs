using System.ComponentModel.DataAnnotations;
using ManagementService.Domain.Enums;

namespace ManagementService.Domain.Entities;

/// <summary>
/// Уведомление для конкретного пользователя.
/// Хранится в PostgreSQL, доставляется через SignalR в real-time.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>ID получателя</summary>
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public NotificationType Type { get; set; }

    /// <summary>Короткий заголовок ("Вам назначена задача")</summary>
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Тело уведомления (название задачи, имя воркспейса и т.д.)</summary>
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>ID связанного объекта (taskId, workspaceId, memberId)</summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>Дополнительный ID (projectId для задач, invitationId для инвайтов)</summary>
    public Guid? SecondaryReferenceId { get; set; }

    /// <summary>ID того кто вызвал уведомление (назначил задачу, пригласил)</summary>
    public Guid? ActorId { get; set; }

    public bool IsRead { get; set; } = false;

    /// <summary>Для actionable уведомлений (инвайт): принято/отклонено/ожидает</summary>
    [MaxLength(50)]
    public string? ActionStatus { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
