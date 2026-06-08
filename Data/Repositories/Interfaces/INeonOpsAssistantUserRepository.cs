using IntranetPrueba.Data.Repositories.Models;

namespace IntranetPrueba.Data.Repositories.Interfaces;

public interface INeonOpsAssistantUserRepository
{
    Task<IReadOnlyList<NeonOpsAssistantUserRow>> GetUsersAsync(
        bool onlyActive,
        CancellationToken cancellationToken = default);
}
