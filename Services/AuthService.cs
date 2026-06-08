using IntranetPrueba.Data.Entities;
using IntranetPrueba.Data.Repositories.Interfaces;
using IntranetPrueba.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace IntranetPrueba.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        try
        {
            var user = await _userRepository.GetByUsernameAsync(username, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            if (!_passwordService.VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }

            user.LastLoginAtUtc = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user, cancellationToken);

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando credenciales para el usuario {Username}.", username);
            return null;
        }
    }
}
