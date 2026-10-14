using Collaby.InternalContracts.Events;

namespace UserService.Application.Interfaces;

public interface IIdentityDirectoryBootstrapClient
{
    Task<IdentityDirectoryExportPage> ExportAsync(string? cursor, int limit, CancellationToken cancellationToken = default);
}

public sealed record IdentityDirectoryExportPage(IReadOnlyList<IdentityUserDirectoryEventV1> Items, string? NextCursor);
