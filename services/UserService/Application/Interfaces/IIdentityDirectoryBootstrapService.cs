namespace UserService.Application.Interfaces;

public interface IIdentityDirectoryBootstrapService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}
