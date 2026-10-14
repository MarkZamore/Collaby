using ManagementService.Contracts.Landing;

namespace ManagementService.Application.Interfaces;

public interface ILandingDefaultsProvider
{
    Task<LandingContentDocument> GetAsync(string locale, CancellationToken cancellationToken = default);
}
