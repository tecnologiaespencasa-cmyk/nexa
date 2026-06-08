using IntranetPrueba.Data.Entities;

namespace IntranetPrueba.Services.Interfaces;

public interface IAuthService
{
    Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
}
