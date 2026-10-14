using ManagementService.Contracts.Landing;
using ManagementService.Contracts.Responses.Admin;

namespace ManagementService.Application.Interfaces;

public interface ILandingContentService
{
    Task<LandingContentDocument> GetPublicAsync(string locale, CancellationToken cancellationToken = default);
    Task<AdminLandingLocaleContentResponse> GetAdminAsync(string locale, CancellationToken cancellationToken = default);
    Task<AdminLandingLocaleContentResponse> SaveAsync(
        string locale,
        LandingContentDocument content,
        Guid? updatedByUserId,
        string? updatedByEmail,
        CancellationToken cancellationToken = default);
}
