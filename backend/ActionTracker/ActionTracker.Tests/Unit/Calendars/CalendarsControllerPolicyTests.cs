using System.Reflection;
using ActionTracker.API.Controllers;
using ActionTracker.Infrastructure.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ActionTracker.Tests.Unit.Calendars;

/// <summary>
/// Every <c>/api/calendars</c> endpoint must carry a <c>Calendars.*</c>
/// permission policy — a new action added without one fails here.
/// </summary>
public class CalendarsControllerPolicyTests
{
    private static readonly Dictionary<string, string> ExpectedPolicies = new()
    {
        [nameof(CalendarsController.GetAll)]     = PermissionPolicies.CalendarsView,
        [nameof(CalendarsController.GetById)]    = PermissionPolicies.CalendarsView,
        [nameof(CalendarsController.GetUsage)]   = PermissionPolicies.CalendarsView,
        [nameof(CalendarsController.Create)]     = PermissionPolicies.CalendarsCreate,
        [nameof(CalendarsController.Update)]     = PermissionPolicies.CalendarsEdit,
        [nameof(CalendarsController.SetDefault)] = PermissionPolicies.CalendarsEdit,
        [nameof(CalendarsController.Restore)]    = PermissionPolicies.CalendarsEdit,
        [nameof(CalendarsController.Delete)]     = PermissionPolicies.CalendarsDelete,
    };

    [Fact]
    public void EveryAction_CarriesItsCalendarsPermissionPolicy()
    {
        var actions = typeof(CalendarsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        actions.Select(a => a.Name).Should().BeEquivalentTo(ExpectedPolicies.Keys,
            "a new endpoint must be added to this test together with its policy");

        foreach (var action in actions)
        {
            var policies = action.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy).ToList();
            policies.Should().Equal(new[] { ExpectedPolicies[action.Name] }, $"{action.Name} must be guarded");
        }

        typeof(CalendarsController).GetCustomAttributes<AllowAnonymousAttribute>().Should().BeEmpty();
        actions.SelectMany(a => a.GetCustomAttributes<AllowAnonymousAttribute>()).Should().BeEmpty();
    }

    [Fact]
    public void PolicyConstants_FollowTheAreaDotActionConvention()
    {
        PermissionPolicies.CalendarsView.Should().Be("Calendars.View");
        PermissionPolicies.CalendarsCreate.Should().Be("Calendars.Create");
        PermissionPolicies.CalendarsEdit.Should().Be("Calendars.Edit");
        PermissionPolicies.CalendarsDelete.Should().Be("Calendars.Delete");
    }
}
