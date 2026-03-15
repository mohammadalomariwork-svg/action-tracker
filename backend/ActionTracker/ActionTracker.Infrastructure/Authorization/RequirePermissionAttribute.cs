using System.Security.Claims;
using ActionTracker.Application.Permissions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionTracker.Infrastructure.Authorization;

/// <summary>
/// Action filter attribute that enforces a single area/action permission.
/// Can be stacked — all attributes must pass for the request to proceed.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _area;
    private readonly string _action;

    public RequirePermissionAttribute(string area, string action)
    {
        _area   = area;
        _action = action;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = Forbidden(_area, _action);
            return;
        }

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IEffectivePermissionService>();

        var granted = await permissionService.HasPermissionAsync(userId, _area, _action);

        if (!granted)
        {
            context.Result = Forbidden(_area, _action);
            return;
        }

        await next();
    }

    private static JsonResult Forbidden(string area, string action) =>
        new JsonResult(new { error = "Access denied", area, action })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
}
