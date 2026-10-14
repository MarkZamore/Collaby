using GamificationService.Application.Interfaces;
using GamificationService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GamificationService.Application.Services;

public sealed class ProjectProgressBootstrapService : IProjectProgressBootstrapService
{
    private const int PageSize = 200;

    private readonly GamificationDbContext _dbContext;
    private readonly IProjectProgressBootstrapClient _client;
    private readonly IProjectProgressProjectionWriter _projectionWriter;
    private readonly ILogger<ProjectProgressBootstrapService> _logger;

    public ProjectProgressBootstrapService(
        GamificationDbContext dbContext,
        IProjectProgressBootstrapClient client,
        IProjectProgressProjectionWriter projectionWriter,
        ILogger<ProjectProgressBootstrapService> logger)
    {
        _dbContext = dbContext;
        _client = client;
        _projectionWriter = projectionWriter;
        _logger = logger;
    }

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var hasEntries = await _dbContext.ProjectProgressSnapshots
            .AsNoTracking()
            .AnyAsync(cancellationToken);

        if (hasEntries)
        {
            return;
        }

        _logger.LogInformation("Project progress projection is empty. Starting bootstrap import from ManagementService.");

        string? cursor = null;
        var imported = 0;

        do
        {
            var page = await _client.ExportAsync(cursor, PageSize, cancellationToken);
            if (page.Items.Count > 0)
            {
                await _projectionWriter.ApplySnapshotsAsync(page.Items, cancellationToken);
                imported += page.Items.Count;
            }

            cursor = page.NextCursor;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        _logger.LogInformation("Project progress bootstrap import completed. Imported {Count} entries.", imported);
    }
}
