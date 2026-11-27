using ActionTracker.API.Models;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Helpers;
using ActionTracker.Application.Features.ProjectPeople;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ActionTracker.API.Filters;

/// <summary>
/// WO-2026-11 P-2b. A change to a workspace's own settings — the workspace itself,
/// its admins, members, Project Managers list, strategic-project approvers,
/// classifications, calendar and strategy link — is for the admins of that
/// workspace and the Admin role only (<see cref="IProjectAccessService.EnsureCanManageWorkspaceAsync"/>);
/// the permission policy (<c>Workspaces.Edit</c> / <c>Delete</c>) still applies first.
/// Anyone else who sees the workspace (<see cref="IWorkspaceAccessResolver"/>: the
/// Admin / PMO roles, its admins and members) gets 403 with the reason; a caller who
/// does not see it gets 404, as the workspace pages answer them.
///
/// Put it on every write action whose route carries a workspace id and name the
/// route parameter. Runs after <see cref="WorkspaceLevelAccessAttribute"/> (a hidden
/// workspace stays 404) and before model validation, so the reason does not depend
/// on the body. A workspace that does not exist passes through. <c>WorkspaceSettingsAccessTests</c>
/// finds every such route by reflection and fails when one is not covered.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WorkspaceSettingsAccessAttribute : TypeFilterAttribute
{
    public WorkspaceSettingsAccessAttribute(string routeParameter = "workspaceId")
        : base(typeof(WorkspaceSettingsAccessFilter))
    {
        RouteParameter = routeParameter;
        Arguments = new object[] { routeParameter };
        // After [WorkspaceLevelAccess] (int.MinValue), before the [ApiController] model-state filter (-2000).
        Order = int.MinValue + 1;
    }

    public string RouteParameter { get; }
}

/// <summary>The filter behind <see cref="WorkspaceSettingsAccessAttribute"/>.</summary>
public sealed class WorkspaceSettingsAccessFilter : IAsyncActionFilter
{
    private readonly string _routeParameter;
    private readonly IProjectAccessService _access;
    private readonly IWorkspaceAccessResolver _resolver;
    private readonly IAppDbContext _db;

    public WorkspaceSettingsAccessFilter(
        string routeParameter, IProjectAccessService access, IWorkspaceAccessResolver resolver, IAppDbContext db)
    {
        _routeParameter = routeParameter;
        _access = access;
        _resolver = resolver;
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue(_routeParameter, out var raw)
            && Guid.TryParse(raw?.ToString(), out var workspaceId))
        {
            try
            {
                await _access.EnsureCanManageWorkspaceAsync(workspaceId);
            }
            catch (KeyNotFoundException)
            {
                // Missing (or hidden — [WorkspaceLevelAccess] already answered that): the action answers as before.
            }
            catch (ProjectAccessDeniedException ex)
            {
                var visible = _db.CurrentUserId is { } userId
                    && (await _resolver.GetVisibleWorkspaceIdsAsync(userId)) is var ids
                    && (ids == null || ids.Contains(workspaceId));
                context.Result = visible
                    ? new ObjectResult(ApiResponse<object>.Fail(ex.Message)) { StatusCode = StatusCodes.Status403Forbidden }
                    : new NotFoundObjectResult(ApiResponse<object>.Fail($"Workspace {workspaceId} not found."));
                return;
            }
        }

        await next();
    }
}
