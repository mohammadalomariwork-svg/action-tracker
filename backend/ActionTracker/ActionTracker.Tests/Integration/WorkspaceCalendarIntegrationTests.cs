using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Models;
using ActionTracker.Application.Features.Auth.DTOs;
using ActionTracker.Application.Features.Calendars.DTOs;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 C-3 through the real pipeline (isolated InMemory database):
/// the workspace calendar endpoints and their policies, and the working-day
/// rule on <c>/api/action-items</c> and the date-change request.
/// Dates are in June 2030 so the tests do not depend on today's weekday:
/// Fri 31 May, Sat 1 Jun, Sun 2 Jun, Mon 3 Jun 2030.
/// </summary>
public class WorkspaceCalendarIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "WsCalendar-Test-2026!";

    private const string AdminEmail      = "wscal.admin@integration.test";
    private const string TeamMemberEmail = "wscal.member@integration.test";

    private const string Saturday = "2030-06-01";
    private const string Monday   = "2030-06-03";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public WorkspaceCalendarIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> SignInAsync(string email, string role)
    {
        await _factory.EnsureUserAsync(email, Password, role);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        var body = await ReadAsync<AuthResponseDto>(response);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    private Task<string> SignInAsAdminAsync()      => SignInAsync(AdminEmail, AppRoles.Admin);
    private Task<string> SignInAsTeamMemberAsync() => SignInAsync(TeamMemberEmail, AppRoles.TeamMember);

    private static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions)!;
    }

    /// <summary>Adds a workspace directly (system context, no query filters in the way).</summary>
    private async Task<Guid> AddWorkspaceAsync(Guid? calendarId = null, bool isPrivate = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workspace = new Workspace
        {
            Id               = Guid.NewGuid(),
            Title            = "C-3 workspace " + Guid.NewGuid().ToString("N")[..6],
            OrganizationUnit = "Integration",
            CreatedAt        = DateTime.UtcNow,
            IsPrivate        = isPrivate,
            WorkCalendarId   = calendarId,
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();
        return workspace.Id;
    }

    /// <summary>Adds an active Monday–Saturday calendar directly and returns its id.</summary>
    private async Task<Guid> AddSixDayCalendarAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var calendar = new WorkCalendar
        {
            Id         = Guid.NewGuid(),
            Name       = "Six-day " + Guid.NewGuid().ToString("N")[..6],
            TimeZoneId = WorkCalendar.DefaultTimeZoneId,
            IsActive   = true,
            CreatedAt  = DateTime.UtcNow,
        };
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var working = day != DayOfWeek.Sunday;
            calendar.Days.Add(new WorkCalendarDay
            {
                WorkCalendarId = calendar.Id,
                DayOfWeek      = day,
                IsWorkingDay   = working,
                StartTime      = working ? new TimeOnly(8, 0) : null,
                EndTime        = working ? new TimeOnly(17, 0) : null,
            });
        }
        db.WorkCalendars.Add(calendar);
        await db.SaveChangesAsync();
        return calendar.Id;
    }

    private static object ActionItemBody(Guid workspaceId, string dueDate, string assigneeId) => new
    {
        title       = "C-3 item",
        workspaceId,
        assigneeIds = new[] { assigneeId },
        priority    = "medium",
        status      = "toDo",
        dueDate,
    };

    // ── GET /api/workspaces/{id}/calendar ─────────────────────────────────────

    [Fact]
    public async Task GetCalendar_AsTeamMember_Returns200_WithTheInheritedDefault()
    {
        var workspaceId = await AddWorkspaceAsync();
        await SignInAsTeamMemberAsync();

        var response = await _client.GetAsync($"/api/workspaces/{workspaceId}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadAsync<WorkspaceCalendarDto>(response);
        body.Data!.CalendarName.Should().Be("KU Standard");
        body.Data.IsInherited.Should().BeTrue();
        body.Data.WorkCalendarId.Should().BeNull();
        body.Data.Days.Should().HaveCount(7);

        // Wire format: dayOfWeek as a string, times HH:mm:ss, dates yyyy-MM-dd.
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"dayOfWeek\":\"monday\"").And.Contain("\"startTime\":\"08:00:00\"");
        json.Should().MatchRegex("\"from\":\"\\d{4}-\\d{2}-\\d{2}\"");
    }

    [Fact]
    public async Task GetCalendar_UnknownOrHiddenPrivateWorkspace_Returns404()
    {
        var privateId = await AddWorkspaceAsync(isPrivate: true);
        await SignInAsAdminAsync(); // Admin is not part of the private workspace

        (await _client.GetAsync($"/api/workspaces/{Guid.NewGuid()}/calendar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/workspaces/{privateId}/calendar")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PutAsJsonAsync($"/api/workspaces/{privateId}/calendar", new { workCalendarId = (Guid?)null }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCalendar_WithoutSignIn_Returns401_AndAReversedWindow_Returns400()
    {
        var workspaceId = await AddWorkspaceAsync();
        _client.DefaultRequestHeaders.Authorization = null;
        (await _client.GetAsync($"/api/workspaces/{workspaceId}/calendar")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await SignInAsTeamMemberAsync();
        (await _client.GetAsync($"/api/workspaces/{workspaceId}/calendar?from=2026-12-31&to=2026-12-01"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── PUT /api/workspaces/{id}/calendar and the options list ────────────────

    [Fact]
    public async Task SetCalendar_WithoutWorkspacesEdit_Returns403_AndOptionsWithoutWorkspacesView_Returns403()
    {
        var calendarId  = await AddSixDayCalendarAsync();
        var workspaceId = await AddWorkspaceAsync();
        await SignInAsTeamMemberAsync();

        (await _client.PutAsJsonAsync($"/api/workspaces/{workspaceId}/calendar", new { workCalendarId = calendarId }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync("/api/workspaces/calendars")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Workspaces.AsNoTracking().SingleAsync(w => w.Id == workspaceId)).WorkCalendarId.Should().BeNull();
    }

    [Fact]
    public async Task SetCalendar_AsAdmin_Returns200_ThenGetShowsIt_AndNullGoesBackToTheDefault()
    {
        var calendarId  = await AddSixDayCalendarAsync();
        var workspaceId = await AddWorkspaceAsync();
        await SignInAsAdminAsync();

        var options = await ReadAsync<List<WorkspaceCalendarOptionDto>>(await _client.GetAsync("/api/workspaces/calendars"));
        options.Data!.Should().Contain(o => o.Id == calendarId);
        options.Data!.First().IsDefault.Should().BeTrue();

        var put = await _client.PutAsJsonAsync($"/api/workspaces/{workspaceId}/calendar", new { workCalendarId = calendarId });
        put.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<WorkspaceCalendarDto>(put)).Data!.CalendarId.Should().Be(calendarId);

        var get = await ReadAsync<WorkspaceCalendarDto>(await _client.GetAsync($"/api/workspaces/{workspaceId}/calendar"));
        get.Data!.WorkCalendarId.Should().Be(calendarId);
        get.Data.IsInherited.Should().BeFalse();

        var reset = await _client.PutAsJsonAsync($"/api/workspaces/{workspaceId}/calendar", new { workCalendarId = (Guid?)null });
        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync<WorkspaceCalendarDto>(reset)).Data!.IsInherited.Should().BeTrue();

        (await _client.PutAsJsonAsync($"/api/workspaces/{workspaceId}/calendar", new { workCalendarId = Guid.NewGuid() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Swagger_ListsTheNewWorkspaceCalendarEndpoints()
    {
        var json = await _client.GetStringAsync("/swagger/v1/swagger.json");

        using var doc = JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        paths.GetProperty("/api/Workspaces/{id}/calendar").TryGetProperty("get", out _).Should().BeTrue();
        paths.GetProperty("/api/Workspaces/{id}/calendar").TryGetProperty("put", out _).Should().BeTrue();
        paths.GetProperty("/api/Workspaces/calendars").TryGetProperty("get", out _).Should().BeTrue();
    }

    // ── The rule on /api/action-items ─────────────────────────────────────────

    [Fact]
    public async Task CreateActionItem_OnASaturday_Returns400WithTheReason_InAMondayToFridayWorkspace()
    {
        var workspaceId = await AddWorkspaceAsync();
        var adminId     = await SignInAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/action-items", ActionItemBody(workspaceId, Saturday, adminId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadAsync<object>(response);
        body.Success.Should().BeFalse();
        body.Message.Should().Be(
            "Due date 2030-06-01 (Saturday) is not a working day in the workspace calendar \"KU Standard\". " +
            "Next working day: 2030-06-03 (Monday).");

        (await _client.PostAsJsonAsync("/api/action-items", ActionItemBody(workspaceId, Monday, adminId)))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateActionItem_OnASaturday_Returns201_InAWorkspaceWhoseCalendarWorksSaturdays()
    {
        var workspaceId = await AddWorkspaceAsync(await AddSixDayCalendarAsync());
        var adminId     = await SignInAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/action-items", ActionItemBody(workspaceId, Saturday, adminId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task DateChangeRequest_ForASaturday_Returns400WithTheReason()
    {
        var workspaceId = await AddWorkspaceAsync();
        var adminId     = await SignInAsAdminAsync();

        Guid itemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = new ActionItem
            {
                Id              = Guid.NewGuid(),
                ActionId        = "ACT-C3-" + Guid.NewGuid().ToString("N")[..4],
                Title           = "Standalone",
                WorkspaceId     = workspaceId,
                IsStandalone    = true,
                Status          = ActionStatus.ToDo,
                Priority        = ActionPriority.Medium,
                DueDate         = new DateTime(2030, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                CreatedByUserId = adminId,
                CreatedAt       = DateTime.UtcNow,
            };
            db.ActionItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await _client.PostAsJsonAsync("/api/action-items/workflow/date-change-request", new
        {
            actionItemId = itemId,
            newDueDate   = Saturday,
            reason       = "Integration test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadAsync<object>(response)).Message.Should().StartWith("Requested due date 2030-06-01 (Saturday) is not a working day");
    }
}
