using IdentityService.Domain.Entities;

namespace IdentityService.Application.Interfaces;

public interface IIdentityDirectoryOutboxWriter
{
    void Enqueue(User user, string lifecycleState);
}
