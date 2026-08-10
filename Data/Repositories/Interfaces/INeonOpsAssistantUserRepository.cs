using Nexa.Data.Repositories.Models;

namespace Nexa.Data.Repositories.Interfaces;

public interface INeonOpsAssistantUserRepository
{
    Task<IReadOnlyList<NeonOpsAssistantUserRow>> GetUsersAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);
}
