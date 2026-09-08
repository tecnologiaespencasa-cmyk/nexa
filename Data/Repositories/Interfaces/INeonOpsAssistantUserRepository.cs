using Nexa.Data.Repositories.Models;

namespace Nexa.Data.Repositories.Interfaces;

public interface INeonOpsAssistantUserRepository
{
    Task<IReadOnlyList<NeonOpsAssistantUserRow>> GetUsersAsync(
        bool onlyActive,
        IReadOnlyCollection<string>? professions = null,
        CancellationToken cancellationToken = default);
}
