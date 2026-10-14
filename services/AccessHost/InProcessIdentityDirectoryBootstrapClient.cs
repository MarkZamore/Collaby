using Collaby.InternalContracts.Events;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces;
using Collaby.ServiceDefaults.SystemAdministration;

namespace AccessHost;

internal sealed class InProcessIdentityDirectoryBootstrapClient : IIdentityDirectoryBootstrapClient
{
    private readonly IdentityDbContext _dbContext;
    private readonly ISystemAdminRoster _systemAdminRoster;

    public InProcessIdentityDirectoryBootstrapClient(
        IdentityDbContext dbContext,
        ISystemAdminRoster systemAdminRoster)
    {
        _dbContext = dbContext;
        _systemAdminRoster = systemAdminRoster;
    }

    public async Task<IdentityDirectoryExportPage> ExportAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        var offset = DecodeCursor(cursor);

        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.Id)
            .Skip(offset)
            .Take(normalizedLimit + 1)
            .Select(user => new IdentityUserDirectoryEventV1(
                user.Id,
                user.Username,
                user.Email,
                user.IsActive,
                false,
                user.IsActive ? "activated" : "deactivated"))
            .ToListAsync(cancellationToken);

        if (users.Count == 0)
        {
            return new IdentityDirectoryExportPage([], null);
        }

        var pageItems = users
            .Take(normalizedLimit)
            .Select(entry => entry with { IsSystemAdmin = _systemAdminRoster.IsSystemAdminEmail(entry.Email) })
            .ToArray();

        var nextCursor = users.Count > normalizedLimit
            ? EncodeCursor(offset + normalizedLimit)
            : null;

        return new IdentityDirectoryExportPage(pageItems, nextCursor);
    }

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return int.TryParse(raw, out var offset) && offset >= 0 ? offset : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset)
    {
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
    }
}
