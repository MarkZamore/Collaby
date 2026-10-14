using Microsoft.EntityFrameworkCore;
using ManagementService.Application.Interfaces;
using ManagementService.Contracts.Responses;
using ManagementService.Infrastructure.Data;

namespace ManagementService.Application.Services;

/// <summary>
/// Агрегирующий сервис для начальной загрузки приложения.
/// Собирает данные параллельно из локальной БД и UserService.
/// При частичной недоступности сервисов ставит Partial = true.
/// </summary>
public class BootstrapService : IBootstrapService
{
    private readonly ManagementDbContext _db;
    private readonly IUserLookupClient _userLookupClient;
    private readonly ILogger<BootstrapService> _logger;

    public BootstrapService(
        ManagementDbContext db,
        IUserLookupClient userLookupClient,
        ILogger<BootstrapService> logger)
    {
        _db = db;
        _userLookupClient = userLookupClient;
        _logger = logger;
    }

    public async Task<BootstrapResponse> GetBootstrapDataAsync(Guid userId, CancellationToken ct = default)
    {
        var response = new BootstrapResponse();

        // ── 1. Последовательно загружаем данные из БД ────────────────────
        // DbContext не поддерживает параллельные операции, поэтому запросы выполняются строго последовательно.

        var workspaces = await _db.Workspaces
            .Where(w => w.Members.Any(m => m.UserId == userId && m.Status == "Active"))
            .Include(w => w.Members)
            .Include(w => w.Projects)
                .ThenInclude(p => p.Statuses)
            .Include(w => w.Projects)
                .ThenInclude(p => p.Labels)
            .AsNoTracking()
            .ToListAsync(ct);

        // Считаем pending инвайты для workspace'ов пользователя
        var userWorkspaceIds = workspaces.Select(w => w.Id).ToList();

        response.PendingInvitesCount = await _db.WorkspaceInvitations
            .CountAsync(i => userWorkspaceIds.Contains(i.WorkspaceId)
                && i.Status == "Pending"
                && i.ExpiresAt > DateTime.UtcNow,
                ct);

        // ── 2. Собираем ID всех участников для enrichment ─────────────────

        var allMembers = workspaces.SelectMany(w => w.Members).ToList();
        var allUserIds = allMembers.Select(m => m.UserId).Distinct().ToList();

        // Добавим текущего пользователя (если он вне членов — не должно произойти, но для надёжности)
        if (!allUserIds.Contains(userId))
            allUserIds.Add(userId);

        // ── 3. Запрос в UserService (enrichment) ──────────────────────────

        IReadOnlyDictionary<Guid, UserLookupResult> profiles = new Dictionary<Guid, UserLookupResult>();

        try
        {
            profiles = await _userLookupClient.GetUsersByIdsAsync(allUserIds, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UserService unavailable during bootstrap for {Count} users", allUserIds.Count);
            response.Partial = true;
            response.Warnings.Add("User profiles unavailable — displaying without enrichment");
        }

        // ── 4. Собираем профиль текущего пользователя ─────────────────────

        if (profiles.TryGetValue(userId, out var myProfile))
        {
            response.UserProfile = new BootstrapUserProfile
            {
                UserId = userId,
                Username = myProfile.Username,
                DisplayName = myProfile.DisplayName,
                AvatarUrl = myProfile.AvatarUrl
            };
        }
        else
        {
            // Если enrichment не удался, возвращаем минимальный профиль без дополнительной информации.
            response.UserProfile = new BootstrapUserProfile { UserId = userId };
        }

        // ── 5. Маппинг workspace → BootstrapWorkspace ────────────────────

        response.Workspaces = workspaces.Select(w =>
        {
            var currentMember = w.Members.FirstOrDefault(m => m.UserId == userId);

            return new BootstrapWorkspace
            {
                Id = w.Id,
                Name = w.Name,
                Description = w.Description,
                OwnerId = w.OwnerId,
                CreatedAt = w.CreatedAt,
                IsArchived = w.IsArchived,
                Role = currentMember?.Role ?? "Member",
                Projects = w.Projects
                    .Where(p => !p.IsArchived)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => new BootstrapProject
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        CreatedById = p.CreatedBy,
                        CreatedAt = p.CreatedAt,
                        IsArchived = p.IsArchived,
                        Statuses = p.Statuses
                            .OrderBy(s => s.Order)
                            .Select(s => new ProjectStatusResponse
                            {
                                Id = s.Id,
                                Name = s.Name,
                                Order = s.Order,
                                Color = s.Color,
                                IsFinal = s.IsFinal
                            }).ToList(),
                        Labels = p.Labels
                            .OrderBy(l => l.Order)
                            .Select(l => new LabelResponse
                            {
                                Id = l.Id,
                                Name = l.Name,
                                Color = l.Color
                            }).ToList(),
                        BoardStickerIds = !string.IsNullOrEmpty(p.BoardStickerIds)
                            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(p.BoardStickerIds)
                            : null
                    }).ToList(),
                Members = w.Members.Where(m => m.Status == "Active").Select(m =>
                {
                    profiles.TryGetValue(m.UserId, out var p);
                    return new BootstrapMember
                    {
                        UserId = m.UserId,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                        IsActive = m.Status == "Active",
                        Username = p?.Username,
                        DisplayName = p?.DisplayName,
                        AvatarUrl = p?.AvatarUrl
                    };
                }).ToList()
            };
        }).ToList();

        return response;
    }
}
