using ActionTracker.API.Models;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.API.Filters;

/// <summary>
/// WO-2026-11 P-2. Keeps a workspace's own data — the routes that take its id —
/// behind the workspace-level rule (a private workspace only for its admins and
/// members). Since P-2 the Workspace query filter also lets project people's
/// workspaces through so their project's rows can join; without this, a lead PM,
/// sponsor, viewer or team member of one project could read the workspace, its
/// member lists or its settings by id.
///
/// Put it on every controller (or action) whose routes carry a workspace id and
/// name the route parameter. A workspace that does not exist passes through, so
/// the action answers as it did before. Runs before model validation, so a
/// hidden workspace is 404 whatever the body. <c>WorkspaceRouteAccessTests</c>
/// finds every route with a workspace id by reflection and fails when one is not
/// covered.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class WorkspaceLevelAccessAttribute : TypeFilterAttribute
{
    public WorkspaceLevelAccessAttribute(string routeParameter = "workspaceId")
        : base(typeof(WorkspaceLevelAccessFilter))
    {
        RouteParameter = routeParameter;
        Arguments = new object[] { routeParameter };
        // Before the [ApiController] model-state filter (-2000).
        Order = int.MinValue;
    }

    public string RouteParameter { get; }
}

/// <summary>The filter behind <see cref="WorkspaceLevelAccessAttribute"/>.</summary>
public sealed class WorkspaceLevelAccessFilter : IAsyncActionFilter
{
    private readonly string _routeParameter;
    private readonly IAppDbContext _db;

    public WorkspaceLevelAccessFilter(string routeParameter, IAppDbContext db)
    {
        _routeParameter = routeParameter;
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.RouteData.Values.TryGetValue(_routeParameter, out var raw)
            && Guid.TryParse(raw?.ToString(), out var workspaceId)
            && !await IsVisibleOrUnknownAsync(workspaceId))
        {
            context.Result = new NotFoundObjectResult(ApiResponse<object>.Fail($"Workspace {workspaceId} not found."));
            return;
        }

        await next();
    }

    private async Task<bool> IsVisibleOrUnknownAsync(Guid workspaceId)
    {
        var all = _db.Workspaces.IgnoreQueryFilters().Where(w => w.Id == workspaceId);
        if (!await all.AnyAsync()) return true;
        return await all.WhereWorkspaceLevelVisibleTo(_db.CurrentUserId).AnyAsync();
    }
}
