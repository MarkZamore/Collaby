using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

/// <summary>
/// Сервис для управления проектами
/// </summary>
public class ProjectService : IProjectService
{
    private readonly ManagementDbContext _context;
    private readonly IWorkspaceService _workspaceService;
    private readonly IProjectProgressSnapshotOutbox _projectProgressSnapshotOutbox;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        ManagementDbContext context,
        IWorkspaceService workspaceService,
        IProjectProgressSnapshotOutbox projectProgressSnapshotOutbox,
        ILogger<ProjectService> logger)
    {
        _context = context;
        _workspaceService = workspaceService;
        _projectProgressSnapshotOutbox = projectProgressSnapshotOutbox;
        _logger = logger;
    }

    /// <summary>
    /// Создать новый проект в workspace и автоматически создать дефолтные статусы
    /// </summary>
    public async Task<Project> CreateProjectAsync(Guid workspaceId, string name, string? description, Guid userId)
    {
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        // Создаем проект
        var project = new Project
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = description,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        // Создаем дефолтные статусы
        await CreateDefaultStatusesAsync(project.Id);

        // Создаем дефолтные метки
        await EnsureDefaultLabelsAsync(project.Id);
        await _projectProgressSnapshotOutbox.EnqueueSnapshotAsync(project.Id);
        await _context.SaveChangesAsync();

        // Возвращаем проект со статусами
        return await _context.Projects
            .Include(p => p.Statuses.OrderBy(s => s.Order))
            .FirstAsync(p => p.Id == project.Id);
    }

    /// <summary>
    /// Получить все проекты workspace
    /// </summary>
    public async Task<IEnumerable<Project>> GetProjectsByWorkspaceAsync(Guid workspaceId, Guid userId)
    {
        // Проверяем доступ к workspace
        var hasAccess = await _workspaceService.HasAccessToWorkspaceAsync(workspaceId, userId);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("No access to workspace");
        }

        return await _context.Projects
            .Include(p => p.Statuses.OrderBy(s => s.Order))
            .Where(p => p.WorkspaceId == workspaceId && !p.IsArchived)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Получить проект по ID
    /// </summary>
    public async Task<Project?> GetProjectByIdAsync(Guid projectId, Guid userId)
    {
        var hasAccess = await HasAccessToProjectAsync(projectId, userId);
        if (!hasAccess)
        {
            return null; // 404 instead of 403
        }

        return await _context.Projects
            .Include(p => p.Statuses.OrderBy(s => s.Order))
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId && !p.IsArchived);
    }

    /// <summary>
    /// Проверить доступ пользователя к проекту через workspace membership
    /// </summary>
    public async Task<bool> HasAccessToProjectAsync(Guid projectId, Guid userId)
    {
        var project = await _context.Projects
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
            return false;

        return await _workspaceService.HasAccessToWorkspaceAsync(project.WorkspaceId, userId);
    }

    /// <summary>
    /// Обновить список стикеров доски
    /// </summary>
    public async Task<Project?> UpdateBoardStickerIdsAsync(Guid projectId, List<string> stickerIds, Guid userId)
    {
        var workspaceId = await GetProjectWorkspaceIdAsync(projectId);
        await _workspaceService.RequireWorkspaceStructurePermissionAsync(workspaceId, userId);

        var project = await _context.Projects
            .Include(p => p.Statuses.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) return null;

        project.BoardStickerIds = JsonSerializer.Serialize(stickerIds);
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return project;
    }

    /// <summary>
    /// Создать дефолтные метки для проекта (если их ещё нет)
    /// </summary>
    public async System.Threading.Tasks.Task EnsureDefaultLabelsAsync(Guid projectId)
    {
        var alreadyHasLabels = await _context.ProjectLabels.AnyAsync(l => l.ProjectId == projectId);
        if (alreadyHasLabels) return;

        var defaults = new[]
        {
            // Приоритет
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Critical",    Color = "#EF4444", Order = 1, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "High",        Color = "#F97316", Order = 2, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Medium",      Color = "#22C55E", Order = 3, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Low",         Color = "#94A3B8", Order = 4, CreatedAt = DateTime.UtcNow },
            // Тип задачи
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Feature",     Color = "#6366F1", Order = 5, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Bug",         Color = "#EF4444", Order = 6, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Improvement", Color = "#3B82F6", Order = 7, CreatedAt = DateTime.UtcNow },
            // Раздел
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Frontend",    Color = "#06B6D4", Order = 8,  CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Backend",     Color = "#10B981", Order = 9,  CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Design",      Color = "#EC4899", Order = 10, CreatedAt = DateTime.UtcNow },
            // Спринт
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sprint 1",    Color = "#8B5CF6", Order = 11, CreatedAt = DateTime.UtcNow },
            new ProjectLabel { Id = Guid.NewGuid(), ProjectId = projectId, Name = "Sprint 2",    Color = "#F59E0B", Order = 12, CreatedAt = DateTime.UtcNow },
        };

        _context.ProjectLabels.AddRange(defaults);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Создать дефолтные статусы для проекта
    /// </summary>
    private async System.Threading.Tasks.Task CreateDefaultStatusesAsync(Guid projectId)
    {
        var defaultStatuses = new[]
        {
            new ProjectStatus
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = "К выполнению",
                Order = 1,
                Color = "#3B82F6", // Яркий синий
                IsFinal = false
            },
            new ProjectStatus
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = "В процессе",
                Order = 2,
                Color = "#F59E0B", // Яркий янтарный
                IsFinal = false
            },
            new ProjectStatus
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Name = "Готово",
                Order = 3,
                Color = "#22C55E", // Яркий зелёный
                IsFinal = true
            }
        };

        _context.ProjectStatuses.AddRange(defaultStatuses);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Удалить проект (только владелец или администратор workspace)
    /// </summary>
    public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId)
    {
        _logger.LogInformation("[DeleteProjectAsync] Starting delete for project {ProjectId} by user {UserId}", projectId, userId);
        
        var project = await _context.Projects
            .Include(p => p.Workspace)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            _logger.LogWarning("[DeleteProjectAsync] Project {ProjectId} not found", projectId);
            return false;
        }

        _logger.LogInformation("[DeleteProjectAsync] Found project {ProjectId} in workspace {WorkspaceId}", projectId, project.WorkspaceId);

        await _workspaceService.RequireWorkspaceStructurePermissionAsync(project.WorkspaceId, userId);

        _logger.LogInformation("[DeleteProjectAsync] Authorization passed, removing project from database");
        await _projectProgressSnapshotOutbox.EnqueueProjectRemovedAsync(projectId);
        _context.Projects.Remove(project);
        
        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("[DeleteProjectAsync] Project {ProjectId} successfully deleted from database", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DeleteProjectAsync] Failed to save changes to database for project {ProjectId}", projectId);
            throw;
        }
        
        return true;
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
