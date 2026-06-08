using IntranetPrueba.Data.Entities;

namespace IntranetPrueba.Data.Repositories.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);
}
