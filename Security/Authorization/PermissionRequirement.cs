using Microsoft.AspNetCore.Authorization;

namespace Nexa.Security.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
        : this([permissionCode ?? throw new ArgumentNullException(nameof(permissionCode))])
    {
    }

    /// <summary>
    /// Requisito satisfecho cuando el usuario tiene al menos uno de los codigos indicados.
    /// </summary>
    public PermissionRequirement(params string[] permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(permissionCodes);

        var normalized = permissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Debe indicar al menos un codigo de permiso.", nameof(permissionCodes));
        }

        PermissionCodes = normalized;
    }

    public IReadOnlyList<string> PermissionCodes { get; }

    public string PermissionCode => PermissionCodes[0];
}
