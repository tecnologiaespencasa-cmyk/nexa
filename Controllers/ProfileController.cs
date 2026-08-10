using System.Security.Claims;
using Nexa.Models.ViewModels;
using Nexa.Services.Interfaces;
using Nexa.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexa.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private const string DefaultProfileSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="Foto de perfil predeterminada">
          <rect width="64" height="64" rx="32" fill="#eef5ff"/>
          <circle cx="32" cy="25" r="11" fill="none" stroke="#006be6" stroke-width="3"/>
          <path d="M13 55c2-11 10-17 19-17s17 6 19 17" fill="none" stroke="#006be6" stroke-linecap="round" stroke-width="3"/>
        </svg>
        """;

    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await BuildIndexViewModelAsync(cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeEmail(
        [Bind(Prefix = "EmailChange")] ChangeProfileEmailViewModel emailChange,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, emailChange: emailChange));
        }

        var result = await _profileService.ChangeEmailAsync(
            GetCurrentUserId(),
            emailChange.NewEmail,
            emailChange.CurrentPassword,
            GetClientIpAddress(),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar el correo.");
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, emailChange: emailChange));
        }

        await RefreshEmailClaimAsync(emailChange.NewEmail.Trim());
        TempData["SuccessMessage"] = "Tu correo electrónico fue actualizado correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "PasswordChange")] ChangeProfilePasswordViewModel passwordChange,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, passwordChange: passwordChange));
        }

        var result = await _profileService.ChangePasswordAsync(
            GetCurrentUserId(),
            passwordChange.CurrentPassword,
            passwordChange.NewPassword,
            GetClientIpAddress(),
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar la contraseña.");
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, passwordChange: passwordChange));
        }

        TempData["SuccessMessage"] = "Tu contraseña fue actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [RequestSizeLimit(5 * 1024 * 1024 + 64 * 1024)]
    public async Task<IActionResult> ChangePhoto(
        [Bind(Prefix = "PhotoChange")] ChangeProfilePhotoViewModel photoChange,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, photoChange: photoChange));
        }

        Stream? photoStream = photoChange.Photo?.OpenReadStream();
        ServiceResult result;
        try
        {
            result = await _profileService.ChangePhotoAsync(
                GetCurrentUserId(),
                photoStream,
                photoChange.Photo?.Length ?? 0,
                photoChange.HorizontalPosition,
                photoChange.VerticalPosition,
                photoChange.Zoom,
                GetClientIpAddress(),
                cancellationToken);
        }
        finally
        {
            if (photoStream is not null)
            {
                await photoStream.DisposeAsync();
            }
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "No fue posible actualizar la foto.");
            return View("Index", await BuildIndexViewModelAsync(cancellationToken, photoChange: photoChange));
        }

        TempData["SuccessMessage"] = "Tu foto de perfil fue actualizada correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Photo(CancellationToken cancellationToken)
    {
        var photo = await _profileService.GetPhotoAsync(GetCurrentUserId(), cancellationToken);
        return photo is { Length: > 0 }
            ? File(photo, "image/jpeg")
            : Content(DefaultProfileSvg, "image/svg+xml");
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> PhotoSource(CancellationToken cancellationToken)
    {
        var photo = await _profileService.GetPhotoSourceAsync(GetCurrentUserId(), cancellationToken);
        return photo is { Length: > 0 }
            ? File(photo, "image/jpeg")
            : Content(DefaultProfileSvg, "image/svg+xml");
    }

    private async Task<ProfileIndexViewModel> BuildIndexViewModelAsync(
        CancellationToken cancellationToken,
        ChangeProfileEmailViewModel? emailChange = null,
        ChangeProfilePasswordViewModel? passwordChange = null,
        ChangeProfilePhotoViewModel? photoChange = null)
    {
        var profile = await _profileService.GetProfileAsync(GetCurrentUserId(), cancellationToken);
        if (!profile.Succeeded || profile.Value is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return new ProfileIndexViewModel();
        }

        return new ProfileIndexViewModel
        {
            FullName = profile.Value.FullName,
            Username = profile.Value.Username,
            Email = profile.Value.Email,
            HasProfilePhoto = profile.Value.HasProfilePhoto,
            RoleName = GetCurrentUserRoles(),
            EmailChange = emailChange ?? new(),
            PasswordChange = passwordChange ?? new(),
            PhotoChange = photoChange ?? new()
            {
                HorizontalPosition = profile.Value.ProfilePhotoHorizontalPosition,
                VerticalPosition = profile.Value.ProfilePhotoVerticalPosition,
                Zoom = profile.Value.ProfilePhotoZoom
            }
        };
    }

    private async Task RefreshEmailClaimAsync(string email)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = authenticateResult.Principal?.Identity as ClaimsIdentity;
        if (identity is null)
        {
            return;
        }

        var previousEmailClaim = identity.FindFirst(ClaimTypes.Email);
        if (previousEmailClaim is not null)
        {
            identity.RemoveClaim(previousEmailClaim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authenticateResult.Principal!,
            authenticateResult.Properties);
    }

    private Guid GetCurrentUserId()
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : Guid.Empty;
    }

    private string GetCurrentUserRoles()
    {
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(" · ", roles.DefaultIfEmpty("Sin rol asignado"));
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
