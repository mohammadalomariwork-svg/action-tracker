using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using ActionTracker.API.Filters;
using ActionTracker.Application.Features.ProjectPeople;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
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
/// WO-2026-11 P-2b. Every write route that takes a workspace id — the workspace
/// itself, its admins, members, Project Managers list, approvers, classifications,
/// calendar, strategy link — is for that workspace's admins and the Admin role only.
/// Finds the routes by reflection: a new one without <c>[WorkspaceSettingsAccess]</c>
/// fails here. Each is called by a member who holds every Workspaces permission
/// (Workspace Admin role) but is not an admin of that workspace (403 with the
/// reason), by a non-member with the same role (404, the workspace pages hide it),
/// by the workspace's own admin and by the Admin role (neither 403 nor 404).
/// </summary>
public class WorkspaceSettingsAccessTests : ProjectPeopleIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    private readonly ITestOutputHelper _out;

    public WorkspaceSettingsAccessTests(ActionTrackerWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
        => _out = output;

    private sealed record Route(string Method, string Template, ControllerActionDescriptor Action);

    private List<Route> WorkspaceWriteRoutes()
    {
        var provider = Factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
        return provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(a => a.AttributeRouteInfo?.Template is { } t
                && (t.Contains("{workspaceId", StringComparison.OrdinalIgnoreCase)
                    || (a.ControllerName == "Workspaces" && t.Contains("{id", StringComparison.OrdinalIgnoreCase))))
            .SelectMany(a => (a.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods)
                              ?? new[] { "GET" })
                .Where(m => m != "GET")
                .Select(m => new Route(m, a.AttributeRouteInfo!.Template!, a)))
            .OrderBy(r => r.Template).ThenBy(r => r.Method)
            .ToList();
    }

    [Fact]
    public void Every_workspace_write_route_carries_the_settings_filter()
    {
        var routes = WorkspaceWriteRoutes();
        routes.Count.Should().BeGreaterThanOrEqualTo(17, "13 workspace routes and 4 classification routes write settings");
        var missing = routes
            .Where(r => r.Action.MethodInfo.GetCustomAttribute<WorkspaceSettingsAccessAttribute>() is not { } attr
                || !r.Template.Contains("{" + attr.RouteParameter, StringComparison.OrdinalIgnoreCase))
            .Select(r => $"{r.Method} {r.Template}")
            .ToList();
        missing.Should().BeEmpty("a write to a workspace's settings must check that the caller is one of its admins or an Admin");
    }

    private async Task<(Guid Workspace, string OtherUserId, Guid ClassificationId, Guid OrgUnitId)> SeedAsync(
        Session admin, Session member, Session other)
    {
        var ws = await AddWorkspaceAsync("P-2b settings", [admin.UserId], [member.UserId, other.UserId]);
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
        var orgUnit = new OrgUnit { Id = Guid.NewGuid(), Name = "P-2b unit " + Guid.NewGuid().ToString("N")[..6], Level = 2, CreatedAt = DateTime.UtcNow };
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
    public async Task Only_a_workspaces_admins_and_the_Admin_role_change_its_settings()
    {
        var n = Guid.NewGuid().ToString("N")[..6];
        var wsAdmin   = await SignInAsync($"p2b.set.wsadmin.{n}", AppRoles.WorkspaceAdmin);
        var member    = await SignInAsync($"p2b.set.member.{n}", AppRoles.WorkspaceAdmin);    // every Workspaces permission, member only
        var stranger  = await SignInAsync($"p2b.set.stranger.{n}", AppRoles.WorkspaceAdmin);  // the same role, not in the workspace
        var adminRole = await SignInAsync($"p2b.set.admin.{n}", AppRoles.Admin);
        var other     = await SignInAsync($"p2b.set.other.{n}", AppRoles.TeamMember);

        var routes = WorkspaceWriteRoutes();
        var wrong = new List<string>();
        foreach (var route in routes)
        {
            var method = new HttpMethod(route.Method);
            object? Body(Guid orgUnitId) => method == HttpMethod.Delete ? null
                : route.Template.EndsWith("from-org-unit", StringComparison.OrdinalIgnoreCase) ? new { orgUnitId }
                : new { };

            // The member and the stranger change nothing, so they share a workspace with the control calls.
            var (ws, userId, classificationId, orgUnitId) = await SeedAsync(wsAdmin, member, other);
            var url = Fill(route.Template, ws, userId, classificationId);

            var asMember = await SendAsync(method, url, member, Body(orgUnitId));
            var reason = await MessageAsync(asMember);
            if (asMember.StatusCode != HttpStatusCode.Forbidden || reason != ProjectAccessDeniedException.WorkspaceAdminsOnly)
                wrong.Add($"member {route.Method} {route.Template} → {(int)asMember.StatusCode} {reason}");

            var asStranger = await StatusAsync(method, url, stranger, Body(orgUnitId));
            if (asStranger != HttpStatusCode.NotFound)
                wrong.Add($"stranger {route.Method} {route.Template} → {(int)asStranger}");

            var asWsAdmin = await StatusAsync(method, url, wsAdmin, Body(orgUnitId));
            if (asWsAdmin is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                wrong.Add($"workspace admin {route.Method} {route.Template} → {(int)asWsAdmin}");

            var (ws2, userId2, classificationId2, orgUnitId2) = await SeedAsync(wsAdmin, member, other);
            var asAdminRole = await StatusAsync(method, Fill(route.Template, ws2, userId2, classificationId2), adminRole, Body(orgUnitId2));
            if (asAdminRole is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                wrong.Add($"Admin role {route.Method} {route.Template} → {(int)asAdminRole}");

            _out.WriteLine($"{route.Method,-6} {url,-100} member {(int)asMember.StatusCode} stranger {(int)asStranger} ws admin {(int)asWsAdmin} Admin {(int)asAdminRole}");
        }

        wrong.Should().BeEmpty();
    }

    [Fact]
    public async Task The_workspace_responses_say_who_may_change_the_settings()
    {
        var n = Guid.NewGuid().ToString("N")[..6];
        var wsAdmin   = await SignInAsync($"p2b.flag.wsadmin.{n}", AppRoles.WorkspaceAdmin);
        var member    = await SignInAsync($"p2b.flag.member.{n}", AppRoles.WorkspaceAdmin);
        var adminRole = await SignInAsync($"p2b.flag.admin.{n}", AppRoles.Admin);
        var ws = await AddWorkspaceAsync("P-2b flag", [wsAdmin.UserId], [member.UserId]);

        foreach (var (who, expected) in new[] { (wsAdmin, true), (member, false), (adminRole, true) })
        {
            var detail = await ReadAsync<ActionTracker.Application.Features.Workspaces.DTOs.WorkspaceResponseDto>(
                await SendAsync(HttpMethod.Get, $"/api/workspaces/{ws}", who));
            detail.CanManageSettings.Should().Be(expected);
            var list = await ReadAsync<List<ActionTracker.Application.Features.Workspaces.DTOs.WorkspaceListDto>>(
                await SendAsync(HttpMethod.Get, "/api/workspaces", who));
            list.Should().ContainSingle(x => x.Id == ws).Which.CanManageSettings.Should().Be(expected);
            // B2: all three may create here (admin, member, Admin role).
            detail.CanCreateProjects.Should().BeTrue();
            detail.CanCreateActionItems.Should().BeTrue();
        }

        // Someone who reads the public workspace by id but is not part of it may not create there.
        var stranger = await SignInAsync($"p2b.flag.stranger.{n}", AppRoles.WorkspaceAdmin);
        var seen = await ReadAsync<ActionTracker.Application.Features.Workspaces.DTOs.WorkspaceResponseDto>(
            await SendAsync(HttpMethod.Get, $"/api/workspaces/{ws}", stranger));
        seen.CanManageSettings.Should().BeFalse();
        seen.CanCreateProjects.Should().BeFalse();
        seen.CanCreateActionItems.Should().BeFalse();
    }
}
