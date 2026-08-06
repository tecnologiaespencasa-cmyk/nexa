using IntranetPrueba.Services.Models;

namespace IntranetPrueba.Services.Interfaces;

public interface IProfileService
{
    Task<ServiceResult<PersonalProfileDto>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangeEmailAsync(
        Guid userId,
        string newEmail,
        string currentPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ServiceResult> ChangePhotoAsync(
        Guid userId,
        Stream photoStream,
        long photoLength,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<byte[]?> GetPhotoAsync(Guid userId, CancellationToken cancellationToken = default);
}
