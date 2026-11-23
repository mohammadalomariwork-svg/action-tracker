using System.Reflection;
using ActionTracker.API.Controllers;
using ActionTracker.Infrastructure.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ActionTracker.Tests.Unit.Calendars;

/// <summary>
/// The WO-2026-11 C-3 endpoints on <see cref="WorkspacesController"/> carry the
/// agreed policies: change = <c>Workspaces.Edit</c>; options list =
/// <c>Workspaces.View</c>; the calendar read used by every date form = any
/// signed-in user (like assignable-users / project-managers).
/// </summary>
public class WorkspaceCalendarEndpointPolicyTests
{
    [Theory]
    [InlineData(nameof(WorkspacesController.SetCalendar),        "{id:guid}/calendar", "PUT", PermissionPolicies.WorkspacesEdit)]
    [InlineData(nameof(WorkspacesController.GetCalendarOptions), "calendars",          "GET", PermissionPolicies.WorkspacesView)]
    [InlineData(nameof(WorkspacesController.GetCalendar),        "{id:guid}/calendar", "GET", "LocalOrAzureAD")]
    public void Endpoint_CarriesItsPolicy(string action, string template, string verb, string policy)
    {
        var method = typeof(WorkspacesController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!;

        var route = method.GetCustomAttributes()
            .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
            .Single();
        route.Template.Should().Be(template);
        route.HttpMethods.Should().Equal(verb);

        method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).Should().Equal(policy);
        method.GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();
    }

    [Fact]
    public void Controller_RequiresASignedInUser()
    {
        typeof(WorkspacesController).GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy).Should().Contain("LocalOrAzureAD");
        typeof(WorkspacesController).GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();
    }
}
