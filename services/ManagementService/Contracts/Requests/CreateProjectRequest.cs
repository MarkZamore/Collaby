using System.ComponentModel.DataAnnotations;

namespace ManagementService.Contracts.Requests;

/// <summary>
/// Запрос на создание нового проекта
/// </summary>
public class CreateProjectRequest
{
    /// <summary>
    /// Название проекта
    /// </summary>
    [Required]
    [MinLength(1)]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание проекта (опционально)
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}