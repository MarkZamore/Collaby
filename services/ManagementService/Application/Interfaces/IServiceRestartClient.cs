using ManagementService.Domain.Entities;

namespace ManagementService.Application.Interfaces;

public interface IServiceRestartClient
{
    Task TriggerRestartAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default);
    Task TrySyncAsync(ServiceRestartOperation operation, CancellationToken cancellationToken = default);
}
