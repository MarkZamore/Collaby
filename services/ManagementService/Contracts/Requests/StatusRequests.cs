using System.ComponentModel.DataAnnotations;

namespace ManagementService.Contracts.Requests;

/// <summary>
/// Запрос на создание нового статуса проекта
/// </summary>
public class CreateStatusRequest
{
    /// <summary>
    /// Название статуса
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Цвет статуса (HEX)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; } = "#808080";

    /// <summary>
    /// Является ли статус финальным
    /// </summary>
    public bool IsFinal { get; set; } = false;
}

/// <summary>
/// Запрос на обновление статуса проекта
/// </summary>
public class UpdateStatusRequest
{
    /// <summary>
    /// Название статуса
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Цвет статуса (HEX)
    /// </summary>
    [MaxLength(7)]
    public string? Color { get; set; }

    /// <summary>
    /// Является ли статус финальным
    /// </summary>
    public bool IsFinal { get; set; } = false;
}

/// <summary>
/// Запрос на изменение порядка статусов
/// </summary>
public class ReorderStatusesRequest
{
    /// <summary>
    /// Массив ID статусов в новом порядке
    /// </summary>
    [Required]
    public Guid[] StatusIds { get; set; } = Array.Empty<Guid>();
}