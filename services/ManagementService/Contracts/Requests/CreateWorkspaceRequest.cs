namespace ManagementService.Contracts.Requests;

/// <summary>
/// Запрос на создание нового рабочего пространства
/// </summary>
public class CreateWorkspaceRequest
{
    /// <summary>
    /// Название рабочего пространства
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Описание рабочего пространства (опционально)
    /// </summary>
    public string? Description { get; set; }
}
