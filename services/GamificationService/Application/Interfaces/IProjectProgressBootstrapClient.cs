using Collaby.InternalContracts.Events;

namespace GamificationService.Application.Interfaces;

public interface IProjectProgressBootstrapClient
{
    Task<ProjectProgressExportPage> ExportAsync(
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectProgressExportPage(
    IReadOnlyList<ManagementProjectProgressSnapshotEventV1> Items,
    string? NextCursor);
