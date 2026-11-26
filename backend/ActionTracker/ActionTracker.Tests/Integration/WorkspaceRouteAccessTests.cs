using System.Net;
using System.Text.RegularExpressions;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 P-2. Finds every API route that takes a workspace id — a
/// <c>{workspaceId}</c> route value anywhere, or <c>{id}</c> on
/// <c>WorkspacesController</c> — and calls each one on a private workspace as a
/// person whose only link is a role on one of its projects. They hold the Admin
/// system role, so no permission policy stops them first: every route must be
/// 404. The same route is then called by the workspace's own admin on a fresh
/// workspace and must not be 404, which shows the route values and bodies reach
/// the resource. A workspace route added later without
/// <c>[WorkspaceLevelAccess]</c> fails here.
/// </summary>
public class WorkspaceRouteAccessTests : ProjectPeopleIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    private readonly ITestOutputHelper _out;

    public WorkspaceRouteAccessTests(ActionTrackerWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
        => _out = output;

    private sealed record Route(string Method, string Template, string Controller);

    private List<Route> WorkspaceRoutes()
    {
        var provider = Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        return provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.AttributeRouteInfo?.Template is { } t
                && (t.Contains("{workspaceId", StringComparison.OrdinalIgnoreCase)
                    || (a.ControllerName == "Workspaces" && t.Contains("{id", StringComparison.OrdinalIgnoreCase))))
            .SelectMany(a => (a.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods)
                              ?? new[] { "GET" })
                .Select(m => new Route(m, a.AttributeRouteInfo!.Template!, a.ControllerName)))
            .Distinct()
            .OrderBy(r => r.Template).ThenBy(r => r.Method)
            .ToList();
    }

    /// <summary>A private workspace with one member, a PM-list entry, an approver and a classification, and a project with a viewer.</summary>
    private async Task<(Guid Workspace, string UserId, Guid ClassificationId, Guid OrgUnitId)> SeedWorkspaceAsync(Session admin, Session projectViewer, Session other)
    {
        var ws = await AddWorkspaceAsync("P-2 routes", [admin.UserId], [other.UserId], isPrivate: true);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = ws, UserId = other.UserId, UserName = other.UserId });
        db.WorkspaceStrategicProjectApprovers.Add(new WorkspaceStrategicProjectApprover { WorkspaceId = ws, UserId = other.UserId, UserName = other.UserId, Order = 1 });
        var classification = new WorkspaceProjectClassification
        {
            Id = Guid.NewGuid(), WorkspaceId = ws, Name = "Capital " + Guid.NewGuid().ToString("N")[..6], SortOrder = 1, IsActive = true,
        };
        classification.NormalizedName = classification.Name.ToUpperInvariant();
        db.WorkspaceProjectClassifications.Add(classification);
        var project = NewProject(ws, "Route project " + Guid.NewGuid().ToString("N")[..6], admin.UserId);
        project.Sponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = admin.UserId });
        project.People.Add(Member(project.Id, projectViewer.UserId, ProjectMemberRole.Viewer));
        db.Projects.Add(project);
        var orgUnit = new OrgUnit { Id = Guid.NewGuid(), Name = "P-2 unit " + Guid.NewGuid().ToString("N")[..6], Level = 2, CreatedAt = DateTime.UtcNow };
        db.OrgUnits.Add(orgUnit);
        await db.SaveChangesAsync();
        return (ws, other.UserId, classification.Id, orgUnit.Id);
    }

    private static string Fill(string template, Guid ws, string userId, Guid classificationId)
    {
        var url = "/" + template.TrimStart('/');
        url = Regex.Replace(url, @"\{workspaceId(:[^}]*)?\}", ws.ToString(), RegexOptions.IgnoreCase);
        url = Regex.Replace(url, @"\{classificationId(:[^}]*)?\}", classificationId.ToString(), RegexOptions.IgnoreCase);
        url = Regex.Replace(url, @"\{userId(:[^}]*)?\}", userId, RegexOptions.IgnoreCase);
        url = Regex.Replace(url, @"\{id(:[^}]*)?\}", ws.ToString(), RegexOptions.IgnoreCase);
        return Regex.Replace(url, @"\{[^}]+\}", Guid.NewGuid().ToString());
    }

    [Fact]
    public async Task Every_route_that_takes_a_workspace_id_is_404_for_a_project_only_person()
    {
        var n = Guid.NewGuid().ToString("N")[..6];
        var admin  = await SignInAsync($"p2.routes.admin.{n}", AppRoles.Admin);
        var viewer = await SignInAsync($"p2.routes.viewer.{n}", AppRoles.Admin);
        var other  = await SignInAsync($"p2.routes.other.{n}", AppRoles.TeamMember);

        var routes = WorkspaceRoutes();
        routes.Count.Should().BeGreaterThan(25, "the workspace, tag and classification controllers have more routes than this");

        var leaks = new List<string>();
        var unreached = new List<string>();
        foreach (var route in routes)
        {
            // Each route on its own workspace, so writes in the control run cannot affect the next route.
            var (ws, userId, classificationId, orgUnitId) = await SeedWorkspaceAsync(admin, viewer, other);
            var url = Fill(route.Template, ws, userId, classificationId);
            var method = new HttpMethod(route.Method);
            object? body = method == HttpMethod.Get || method == HttpMethod.Delete ? null
                : route.Template.EndsWith("from-org-unit", StringComparison.OrdinalIgnoreCase) ? new { orgUnitId }
                : new { };

            var asViewer = await StatusAsync(method, url, viewer, body);
            _out.WriteLine($"{route.Method,-6} {url,-110} project-only: {(int)asViewer}");
            if (asViewer != HttpStatusCode.NotFound) leaks.Add($"{route.Method} {route.Template} → {(int)asViewer}");

            var asAdmin = await StatusAsync(method, url, admin, body);
            _out.WriteLine($"{"",-6} {"",-110} workspace admin: {(int)asAdmin}");
            if (asAdmin == HttpStatusCode.NotFound) unreached.Add($"{route.Method} {route.Template}");
        }

        leaks.Should().BeEmpty("a project-only person must not reach any of the workspace's own data");
        unreached.Should().BeEmpty("the control run must reach every route, or a 404 above proves nothing");
    }
}
