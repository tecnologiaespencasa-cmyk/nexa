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

        var hasPermission = await _currentUserPermissionService.HasPermissionAsync(
            context.User,
            requirement.PermissionCode);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
