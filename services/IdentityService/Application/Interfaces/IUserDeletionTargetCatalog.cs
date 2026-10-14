using IdentityService.Application.Settings;

namespace IdentityService.Application.Interfaces;

public interface IUserDeletionTargetCatalog
{
    IReadOnlyList<UserDeletionTargetDefinition> GetEnabledTargets();
    UserDeletionTargetDefinition? Find(string serviceName);
}
