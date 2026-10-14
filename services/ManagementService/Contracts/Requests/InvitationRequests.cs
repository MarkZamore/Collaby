using System.ComponentModel.DataAnnotations;

namespace ManagementService.Contracts.Requests;

/// <summary>
/// Запрос на создание приглашения в workspace
/// </summary>
public class CreateInvitationRequest
{
    /// <summary>
    /// Email пользователя для приглашения
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Роль, которая будет назначена (Member, Admin)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Срок действия приглашения в днях (по умолчанию 7)
    /// </summary>
    [Range(1, 30)]
    public int ExpiresInDays { get; set; } = 7;
}

/// <summary>
/// Запрос на принятие приглашения
/// </summary>
public class AcceptInvitationRequest
{
    /// <summary>
    /// Токен приглашения
    /// </summary>
    [Required]
    [MinLength(32)]
    public string Token { get; set; } = string.Empty;
}
