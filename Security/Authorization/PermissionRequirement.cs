using Microsoft.AspNetCore.Authorization;

namespace IntranetPrueba.Security.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode?.Trim().ToUpperInvariant()
            ?? throw new ArgumentNullException(nameof(permissionCode));
    }

    public string PermissionCode { get; }
}
