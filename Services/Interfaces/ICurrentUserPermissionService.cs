using System.Security.Claims;

namespace IntranetPrueba.Services.Interfaces;

public interface ICurrentUserPermissionService
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal principal,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
