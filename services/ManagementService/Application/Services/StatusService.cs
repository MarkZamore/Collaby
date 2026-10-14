using Microsoft.EntityFrameworkCore;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

/// <summary>
/// Сервис для управления статусами проектов
/// </summary>
public class StatusService : IStatusService
{
    private readonly ManagementDbContext _context;
    private readonly IProjectService _projectService;
    private readonly IWorkspaceService _workspaceService;

    public StatusService(ManagementDbContext context, IProjectService projectService, IWorkspaceService workspaceService)
    {
        _context = context;
        _projectService = projectService;
        _workspaceService = workspaceService;
    }

    /// <summary>
    /// Получить все статусы проекта
    /// </summary>
    public async Task<IEnumerable<ProjectStatus>> GetProjectStatusesAsync(Guid projectId, Guid userId)
    {
        var workspaceId = await GetProjectWorkspaceIdAsync(projectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        return await _context.ProjectStatuses
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    /// <summary>
    /// Создать новый статус в проекте
    /// </summary>
    public async Task<ProjectStatus> CreateStatusAsync(Guid projectId, string name, string? color, bool isFinal, Guid userId)
    {
        // Проверяем доступ к проекту
        var workspaceId = await GetProjectWorkspaceIdAsync(projectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        // Получаем максимальный порядок для новой позиции
        var maxOrder = await _context.ProjectStatuses
            .Where(s => s.ProjectId == projectId)
            .MaxAsync(s => (int?)s.Order) ?? 0;

        var status = new ProjectStatus
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name,
            Color = color ?? "#808080",
            IsFinal = isFinal,
            Order = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProjectStatuses.Add(status);
        await _context.SaveChangesAsync();

        return status;
    }

    /// <summary>
    /// Обновить статус
    /// </summary>
    public async Task<ProjectStatus?> UpdateStatusAsync(Guid statusId, string name, string? color, bool isFinal, Guid userId)
    {
        var status = await _context.ProjectStatuses
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == statusId);

        if (status == null)
            return null;

        var workspaceId = await GetProjectWorkspaceIdAsync(status.ProjectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        status.Name = name;
        status.Color = color ?? status.Color;
        status.IsFinal = isFinal;

        await _context.SaveChangesAsync();
        return status;
    }

    /// <summary>
    /// Удалить статус
    /// </summary>
    public async Task<bool> DeleteStatusAsync(Guid statusId, Guid userId)
    {
        var status = await _context.ProjectStatuses
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == statusId);

        if (status == null)
            return false;

        var workspaceId = await GetProjectWorkspaceIdAsync(status.ProjectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        // Проверяем, что это не единственный статус в проекте
        var statusCount = await _context.ProjectStatuses
            .CountAsync(s => s.ProjectId == status.ProjectId);

        if (statusCount <= 1)
        {
            throw new InvalidOperationException("Cannot delete the last status in project");
        }

        // TODO: Проверить, что в этом статусе нет задач.
        // var hasActiveTasks = await _context.Tasks.AnyAsync(t => t.StatusId == statusId);
        // if (hasActiveTasks)
        // {
        //     throw new InvalidOperationException("Cannot delete status with active tasks");
        // }

        _context.ProjectStatuses.Remove(status);
        await _context.SaveChangesAsync();

        // Пересчитываем порядок оставшихся статусов
        await ReorderAfterDeleteAsync(status.ProjectId, status.Order);

        return true;
    }

    /// <summary>
    /// Изменить порядок статусов проекта
    /// </summary>
    public async Task<bool> ReorderStatusesAsync(Guid projectId, Guid[] statusIds, Guid userId)
    {
        var workspaceId = await GetProjectWorkspaceIdAsync(projectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        // Получаем все статусы проекта
        var statuses = await _context.ProjectStatuses
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        // Проверяем, что все переданные ID существуют в проекте
        if (statusIds.Length != statuses.Count || 
            !statusIds.All(id => statuses.Any(s => s.Id == id)))
        {
            throw new ArgumentException("Invalid status IDs provided");
        }

        // Обновляем порядок
        for (int i = 0; i < statusIds.Length; i++)
        {
            var status = statuses.First(s => s.Id == statusIds[i]);
            status.Order = i + 1;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Получить статус по ID
    /// </summary>
    public async Task<ProjectStatus?> GetStatusByIdAsync(Guid statusId, Guid userId)
    {
        var status = await _context.ProjectStatuses
            .Include(s => s.Project)
            .FirstOrDefaultAsync(s => s.Id == statusId);

        if (status == null)
            return null;

        // Проверяем доступ к проекту
        var hasAccess = await _projectService.HasAccessToProjectAsync(status.ProjectId, userId);
        if (!hasAccess)
        {
            return null; // 404 instead of 403
        }

        return status;
    }

    /// <summary>
    /// Пересчитать порядок статусов после удаления
    /// </summary>
    private async System.Threading.Tasks.Task ReorderAfterDeleteAsync(Guid projectId, int deletedOrder)
    {
        var statusesToUpdate = await _context.ProjectStatuses
            .Where(s => s.ProjectId == projectId && s.Order > deletedOrder)
            .ToListAsync();

        foreach (var status in statusesToUpdate)
        {
            status.Order -= 1;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<Guid> GetProjectWorkspaceIdAsync(Guid projectId)
    {
        var project = await _context.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.WorkspaceId })
            .FirstOrDefaultAsync();

        if (project == null)
        {
            throw new ArgumentException("Project not found");
        }

        return project.WorkspaceId;
    }
}
