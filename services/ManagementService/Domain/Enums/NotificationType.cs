namespace ManagementService.Domain.Enums;

public enum NotificationType
{
    /// <summary>Задача назначена пользователю</summary>
    TaskAssigned = 0,

    /// <summary>Приглашение в воркспейс (принять/отклонить)</summary>
    WorkspaceInvite = 1,

    /// <summary>Новый комментарий к задаче пользователя</summary>
    CommentAdded = 2,

    /// <summary>Задача обновлена (изменение статуса, описания и т.д.)</summary>
    TaskUpdated = 3,

    /// <summary>Упоминание в комментарии</summary>
    Mention = 4,
}
