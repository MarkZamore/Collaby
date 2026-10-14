using Collaby.InternalContracts.Events;

namespace UserService.Application.Interfaces;

public interface IIdentityDirectoryProjectionWriter
{
    Task ApplyAsync(IReadOnlyCollection<IdentityUserDirectoryEventV1> entries, CancellationToken cancellationToken = default);
}
