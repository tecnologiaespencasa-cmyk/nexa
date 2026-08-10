using Nexa.Data.Repositories.Interfaces;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Nexa.Services;

public class ProfileService : IProfileService
{
    private const long MaximumPhotoInputBytes = 5 * 1024 * 1024;
    private const int MaximumPhotoPixels = 16_000_000;
    private const int PhotoMaximumDimension = 320;

    private readonly IUserAdministrationRepository _repository;
    private readonly IPasswordService _passwordService;
    private readonly IAuditService _auditService;

    public ProfileService(
        IUserAdministrationRepository repository,
        IPasswordService passwordService,
        IAuditService auditService)
    {
        _repository = repository;
        _passwordService = passwordService;
        _auditService = auditService;
    }

    public async Task<ServiceResult<PersonalProfileDto>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<PersonalProfileDto>.Failure("El usuario no existe.");
        }

        return ServiceResult<PersonalProfileDto>.Success(new PersonalProfileDto
        {
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            HasProfilePhoto = user.ProfilePhoto is { Length: > 0 }
        });
    }

    public async Task<ServiceResult> ChangeEmailAsync(
        Guid userId,
        string newEmail,
        string currentPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        if (!_passwordService.VerifyPassword(currentPassword, user.PasswordHash))
        {
            return ServiceResult.Failure("La contraseña actual no es correcta.");
        }

        var email = newEmail.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 150 || !IsValidEmail(email))
        {
            return ServiceResult.Failure("Ingresa un correo electrónico válido.");
        }

        var normalizedEmail = email.ToUpperInvariant();
        if (normalizedEmail == user.NormalizedEmail)
        {
            return ServiceResult.Failure("El correo indicado ya es el correo actual.");
        }

        if (await _repository.ExistsByNormalizedEmailAsync(normalizedEmail, user.Id, cancellationToken))
        {
            return ServiceResult.Failure("Ese correo electrónico ya está registrado.");
        }

        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PROFILE_EMAIL_CHANGED",
            entity: "User",
            details: "El usuario actualizó su correo electrónico.",
            performedByUserId: user.Id,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        if (!_passwordService.VerifyPassword(currentPassword, user.PasswordHash))
        {
            return ServiceResult.Failure("La contraseña actual no es correcta.");
        }

        if (_passwordService.VerifyPassword(newPassword, user.PasswordHash))
        {
            return ServiceResult.Failure("La nueva contraseña debe ser diferente de la actual.");
        }

        user.PasswordHash = _passwordService.HashPassword(newPassword);
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PROFILE_PASSWORD_CHANGED",
            entity: "User",
            details: "El usuario actualizó su contraseña.",
            performedByUserId: user.Id,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ChangePhotoAsync(
        Guid userId,
        Stream photoStream,
        long photoLength,
        int verticalPosition,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        if (photoLength is <= 0 or > MaximumPhotoInputBytes)
        {
            return ServiceResult.Failure("La foto debe pesar como máximo 5 MB.");
        }

        if (verticalPosition is < 0 or > 100)
        {
            return ServiceResult.Failure("El encuadre vertical no es válido.");
        }

        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Failure("El usuario no existe.");
        }

        byte[] optimizedPhoto;
        try
        {
            await using var input = new MemoryStream();
            await photoStream.CopyToAsync(input, cancellationToken);
            input.Position = 0;

            var imageInfo = await Image.IdentifyAsync(input, cancellationToken);
            if (imageInfo is null
                || imageInfo.Width <= 0
                || imageInfo.Height <= 0
                || (long)imageInfo.Width * imageInfo.Height > MaximumPhotoPixels
                || imageInfo.Metadata.DecodedImageFormat?.Name is not ("JPEG" or "PNG" or "WEBP"))
            {
                return ServiceResult.Failure("Selecciona una imagen JPG, PNG o WEBP válida de hasta 4.000 x 4.000 px.");
            }

            input.Position = 0;

            using var image = await Image.LoadAsync<Rgba32>(input, cancellationToken);
            image.Mutate(context => context.AutoOrient());

            var scale = Math.Max(
                (double)PhotoMaximumDimension / image.Width,
                (double)PhotoMaximumDimension / image.Height);
            var resizedWidth = Math.Max(PhotoMaximumDimension, (int)Math.Ceiling(image.Width * scale));
            var resizedHeight = Math.Max(PhotoMaximumDimension, (int)Math.Ceiling(image.Height * scale));

            image.Mutate(context => context.Resize(resizedWidth, resizedHeight));

            var cropX = (image.Width - PhotoMaximumDimension) / 2;
            var cropY = (int)Math.Round(
                (image.Height - PhotoMaximumDimension) * (verticalPosition / 100d),
                MidpointRounding.AwayFromZero);
            image.Mutate(context => context.Crop(new Rectangle(
                cropX,
                cropY,
                PhotoMaximumDimension,
                PhotoMaximumDimension)));

            await using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 70 }, cancellationToken);
            optimizedPhoto = output.ToArray();
        }
        catch (UnknownImageFormatException)
        {
            return ServiceResult.Failure("Selecciona una imagen JPG, PNG o WEBP válida.");
        }
        catch (InvalidImageContentException)
        {
            return ServiceResult.Failure("La imagen está dañada o no puede procesarse.");
        }

        user.ProfilePhoto = optimizedPhoto;
        await _repository.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            action: "PROFILE_PHOTO_CHANGED",
            entity: "User",
            details: $"El usuario actualizó su foto de perfil ({optimizedPhoto.Length / 1024} KB optimizada).",
            performedByUserId: user.Id,
            ipAddress: ipAddress,
            cancellationToken: cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<byte[]?> GetPhotoAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(userId, cancellationToken);
        return user?.ProfilePhoto;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new System.Net.Mail.MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
