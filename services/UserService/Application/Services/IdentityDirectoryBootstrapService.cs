using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Data;

namespace UserService.Application.Services;

public sealed class IdentityDirectoryBootstrapService : IIdentityDirectoryBootstrapService
{
    private const int PageSize = 200;

    private readonly UserDbContext _dbContext;
    private readonly IIdentityDirectoryBootstrapClient _client;
    private readonly IIdentityDirectoryProjectionWriter _projectionWriter;
    private readonly ILogger<IdentityDirectoryBootstrapService> _logger;

    public IdentityDirectoryBootstrapService(
        UserDbContext dbContext,
        IIdentityDirectoryBootstrapClient client,
        IIdentityDirectoryProjectionWriter projectionWriter,
        ILogger<IdentityDirectoryBootstrapService> logger)
    {
        _dbContext = dbContext;
        _client = client;
        _projectionWriter = projectionWriter;
        _logger = logger;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var hasEntries = await _dbContext.IdentityDirectoryEntries
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (hasEntries)
        {
            return;
        }

        _logger.LogInformation("Identity directory projection is empty. Starting bootstrap import from IdentityService.");

        string? cursor = null;
        var imported = 0;

        do
        {
            var page = await _client.ExportAsync(cursor, PageSize, cancellationToken);
            if (page.Items.Count > 0)
            {
                await _projectionWriter.ApplyAsync(page.Items, cancellationToken);
                imported += page.Items.Count;
            }

            cursor = page.NextCursor;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        _logger.LogInformation("Identity directory bootstrap import completed. Imported {Count} entries.", imported);
    }
}
