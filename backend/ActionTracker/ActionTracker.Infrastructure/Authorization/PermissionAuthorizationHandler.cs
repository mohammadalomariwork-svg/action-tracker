using System.Security.Claims;
using ActionTracker.Application.Permissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Authorization;

/// <summary>
/// Handles <see cref="PermissionRequirement"/> by delegating to
/// <see cref="IEffectivePermissionService"/>.
/// Must be registered as a <b>scoped</b> service because it depends on
/// the scoped DbContext via <see cref="IEffectivePermissionService"/>.
/// </summary>
public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly IEffectivePermissionService _permissionService;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IEffectivePermissionService permissionService,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _permissionService = permissionService;
        _logger            = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Extract user ID from standard NameIdentifier or OIDC "sub" claim.
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "PermissionAuthorizationHandler: no user-ID claim found for area '{Area}' action '{Action}'.",
                requirement.Area, requirement.Action);
            context.Fail();
            return;
        }

        var allowed = await _permissionService.HasPermissionAsync(
            userId, requirement.Area, requirement.Action);

        if (allowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug(
                "PermissionAuthorizationHandler: user '{UserId}' denied for '{Area}.{Action}'.",
                userId, requirement.Area, requirement.Action);
            context.Fail();
        }
    }
}
