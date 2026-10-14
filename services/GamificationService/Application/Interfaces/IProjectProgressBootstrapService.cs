namespace GamificationService.Application.Interfaces;

public interface IProjectProgressBootstrapService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}
