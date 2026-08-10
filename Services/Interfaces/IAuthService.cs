using Nexa.Data.Entities;

namespace Nexa.Services.Interfaces;

public interface IAuthService
{
    Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
}
