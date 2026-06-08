using System.Security.Claims;
using IntranetPrueba.Models.ViewModels;
using IntranetPrueba.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntranetPrueba.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;

    public AuthController(IAuthService authService, IAuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _authService.ValidateCredentialsAsync(model.Username, model.Password, cancellationToken);
        if (user is null)
        {
            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entity: "Auth",
                details: $"Intento fallido para usuario {model.Username}",
                performedByUserId: null,
                ipAddress: GetClientIpAddress(),
                cancellationToken: cancellationToken);

            ModelState.AddModelError(string.Empty, "Credenciales inv\u00e1lidas.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("full_name", user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        var roleClaims = user.UserRoles
            .Select(ur => ur.Role.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(roleName => new Claim(ClaimTypes.Role, roleName));
        claims.AddRange(roleClaims);

        var permissionClaims = user.UserPermissions
            .Select(up => up.Permission.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(permission => new Claim("permission", permission));
        claims.AddRange(permissionClaims);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 : 8)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        await _auditService.LogAsync(
            action: "LOGIN_SUCCESS",
            entity: "Auth",
            details: $"Inicio de sesi\u00f3n exitoso de {user.Username}",
            performedByUserId: user.Id,
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        await _auditService.LogAsync(
            action: "LOGOUT",
            entity: "Auth",
            details: $"Cierre de sesi\u00f3n de {User.Identity?.Name}",
            performedByUserId: userId == Guid.Empty ? null : userId,
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> AccessDenied(CancellationToken cancellationToken)
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        await _auditService.LogAsync(
            action: "ACCESS_DENIED",
            entity: "Authorization",
            details: $"Acceso denegado a la ruta {HttpContext.Request.Path}",
            performedByUserId: userId == Guid.Empty ? null : userId,
            ipAddress: GetClientIpAddress(),
            cancellationToken: cancellationToken);

        return View();
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
