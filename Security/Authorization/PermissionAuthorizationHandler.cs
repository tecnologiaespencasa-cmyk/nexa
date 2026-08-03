using IntranetPrueba.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace IntranetPrueba.Security.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserPermissionService _currentUserPermissionService;

    public PermissionAuthorizationHandler(ICurrentUserPermissionService currentUserPermissionService)
    {
        _currentUserPermissionService = currentUserPermissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        foreach (var permissionCode in requirement.PermissionCodes)
        {
            var hasPermission = await _currentUserPermissionService.HasPermissionAsync(
                context.User,
                permissionCode);

            if (hasPermission)
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
