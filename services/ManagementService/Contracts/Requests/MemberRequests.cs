namespace ManagementService.Contracts.Requests;

/// <summary>
/// Запрос на добавление участника в workspace
/// </summary>
public class AddMemberRequest
{
    /// <summary>
    /// ID пользователя для добавления
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Роль участника (Owner, Admin, Member, Guest)
    /// </summary>
    public string Role { get; set; } = "Member";
}

/// <summary>
/// Запрос на обновление роли участника
/// </summary>
public class UpdateMemberRoleRequest
{
    /// <summary>
    /// Новая роль участника
    /// </summary>
    public string Role { get; set; } = string.Empty;
}