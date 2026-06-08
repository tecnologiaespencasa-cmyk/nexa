using System.Security.Claims;
using IntranetPrueba.Data;
using IntranetPrueba.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IntranetPrueba.Services;

public class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private const string PermissionCacheKey = "__CurrentUserPermissionCodes";
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserPermissionService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(principal, out var userId))
        {
            return false;
        }

        var normalizedPermission = permissionCode.Trim().ToUpperInvariant();
        var permissionCodes = await GetCurrentUserPermissionCodesAsync(userId, cancellationToken);
        return permissionCodes.Contains(normalizedPermission);
    }

    private async Task<HashSet<string>> GetCurrentUserPermissionCodesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue(PermissionCacheKey, out var cachedCodes) == true
            && cachedCodes is HashSet<string> cachedPermissions)
        {
            return cachedPermissions;
        }

        var isActive = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (!isActive)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var directPermissions = _context.UserPermissions
            .AsNoTracking()
            .Where(up => up.UserId == userId)
            .Select(up => up.Permission.Code);

        var permissionList = await directPermissions.ToListAsync(cancellationToken);

        var normalizedPermissions = permissionList
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant());

        var permissionSet = new HashSet<string>(normalizedPermissions, StringComparer.OrdinalIgnoreCase);
        if (httpContext is not null)
        {
            httpContext.Items[PermissionCacheKey] = permissionSet;
        }

        return permissionSet;
    }

    private static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId)
    {
        var userIdValue = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdValue, out userId);
    }
}
