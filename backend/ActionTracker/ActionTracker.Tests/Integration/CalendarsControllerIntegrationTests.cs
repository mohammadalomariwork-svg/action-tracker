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
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// Integration tests for <c>/api/calendars</c>: the seeded default calendar,
/// the permission policy on each verb (401 / 403), and the 409 guards.
/// Runs the real pipeline against an isolated InMemory database.
/// </summary>
public class CalendarsControllerIntegrationTests
    : IClassFixture<ActionTrackerWebApplicationFactory>
{
    private const string Password = "Calendar-Test-2026!";

    private const string AdminEmail          = "calendar.admin@integration.test";
    private const string WorkspaceAdminEmail = "calendar.wsadmin@integration.test";
    private const string TeamMemberEmail     = "calendar.member@integration.test";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ActionTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CalendarsControllerIntegrationTests(ActionTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SignInAsync(string email, string role)
    {
        await _factory.EnsureUserAsync(email, Password, role);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        var body = await ReadAsync<AuthResponseDto>(response);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Data!.AccessToken);
    }

    private Task SignInAsAdminAsync()          => SignInAsync(AdminEmail, AppRoles.Admin);
    private Task SignInAsWorkspaceAdminAsync() => SignInAsync(WorkspaceAdminEmail, AppRoles.WorkspaceAdmin);
    private Task SignInAsTeamMemberAsync()     => SignInAsync(TeamMemberEmail, AppRoles.TeamMember);

    private static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse<T>>(json, JsonOptions)!;
    }

    /// <summary>Request body for a Monday–Friday 08:00–17:00 calendar.</summary>
    private static object CalendarBody(string name, bool isActive = true, int dayCount = 7) => new
    {
        name,
        timeZoneId = "Asia/Dubai",
        isActive,
        days = Enum.GetValues<DayOfWeek>()
            .Take(dayCount)
            .Select(d =>
            {
                var working = d is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
                return new
                {
                    dayOfWeek    = d.ToString(),
                    isWorkingDay = working,
                    startTime    = working ? "08:00:00" : null,
                    endTime      = working ? "17:00:00" : null,
                };
            }),
        exceptions = new[]
        {
            new { name = "National Day", startDate = "2026-12-02", endDate = "2026-12-03", isWorkingDay = false },
        },
    };

    private async Task<WorkCalendarDto> CreateCalendarAsync(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/calendars", CalendarBody(name));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await ReadAsync<WorkCalendarDto>(response)).Data!;
    }

    private async Task<List<WorkCalendarDto>> ListCalendarsAsync()
    {
        var response = await _client.GetAsync("/api/calendars");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await ReadAsync<List<WorkCalendarDto>>(response)).Data!;
    }

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..30];

    // ── Seeding ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_OnACleanDatabase_ReturnsTheSeededDefaultCalendar()
    {
        await SignInAsAdminAsync();

        var calendars = await ListCalendarsAsync();

        calendars.Should().ContainSingle(c => c.IsDefault);
        var seeded = calendars.Single(c => c.Name == "KU Standard");
        seeded.TimeZoneId.Should().Be("Asia/Dubai");
        seeded.Days.Should().HaveCount(7);
        seeded.Days.Where(d => d.IsWorkingDay).Select(d => d.DayOfWeek).Should().BeEquivalentTo(new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        });
        seeded.Days.Where(d => d.IsWorkingDay).Should().OnlyContain(d =>
            d.StartTime == new TimeOnly(8, 0) && d.EndTime == new TimeOnly(17, 0));
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WithoutAToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/calendars");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EveryEndpoint_ForAUserWithoutCalendarPermissions_Returns403()
    {
        await SignInAsTeamMemberAsync();
        var id = Guid.NewGuid();

        (await _client.GetAsync("/api/calendars")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/calendars/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/calendars/{id}/usage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PostAsJsonAsync("/api/calendars", CalendarBody(UniqueName("Denied"))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsJsonAsync($"/api/calendars/{id}", CalendarBody(UniqueName("Denied"))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsync($"/api/calendars/{id}/default", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PatchAsync($"/api/calendars/{id}/restore", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.DeleteAsync($"/api/calendars/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task WorkspaceAdmin_CanView_ButCannotChangeCalendars()
    {
        await SignInAsWorkspaceAdminAsync();
        var id = Guid.NewGuid();

        (await _client.GetAsync("/api/calendars")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsJsonAsync("/api/calendars", CalendarBody(UniqueName("Denied"))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.PutAsync($"/api/calendars/{id}/default", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.DeleteAsync($"/api/calendars/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Swagger ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Swagger_ListsEveryCalendarEndpoint()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        string[] Verbs(string path) =>
            paths.GetProperty(path).EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

        Verbs("/api/calendars").Should().Equal("get", "post");
        Verbs("/api/calendars/{id}").Should().Equal("delete", "get", "put");
        Verbs("/api/calendars/{id}/usage").Should().Equal("get");
        Verbs("/api/calendars/{id}/default").Should().Equal("put");
        Verbs("/api/calendars/{id}/restore").Should().Equal("patch");
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ThenGet_ReturnsTheCalendarWithDaysAndExceptions()
    {
        await SignInAsAdminAsync();

        var created = await CreateCalendarAsync(UniqueName("Site Office"));

        var response = await _client.GetAsync($"/api/calendars/{created.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var fetched = (await ReadAsync<WorkCalendarDto>(response)).Data!;
        fetched.IsDefault.Should().BeFalse("a default already exists");
        fetched.Days.Should().HaveCount(7);
        fetched.Exceptions.Should().ContainSingle(e =>
            e.Name == "National Day"
            && e.StartDate == new DateOnly(2026, 12, 2)
            && e.EndDate == new DateOnly(2026, 12, 3)
            && !e.IsWorkingDay);
    }

    [Fact]
    public async Task Create_WithAnIncompleteWeek_Returns400()
    {
        await SignInAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/calendars", CalendarBody(UniqueName("Broken"), dayCount: 6));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithADuplicateName_Returns409()
    {
        await SignInAsAdminAsync();

        var response = await _client.PostAsJsonAsync("/api/calendars", CalendarBody("KU Standard"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_UnknownCalendar_Returns404()
    {
        await SignInAsAdminAsync();

        var response = await _client.GetAsync($"/api/calendars/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ThenRestore_RoundTrips()
    {
        await SignInAsAdminAsync();
        var created = await CreateCalendarAsync(UniqueName("Temporary"));

        (await _client.DeleteAsync($"/api/calendars/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/calendars/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await _client.PatchAsync($"/api/calendars/{created.Id}/restore", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/calendars/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Guards (409) ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ACalendarInUseByAWorkspace_Returns409_AndUsageListsTheWorkspace()
    {
        await SignInAsAdminAsync();
        var created = await CreateCalendarAsync(UniqueName("In use"));

        var workspaceId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Workspaces.Add(new Workspace
            {
                Id               = workspaceId,
                Title            = "Calendar consumer",
                OrganizationUnit = "Org",
                CreatedAt        = DateTime.UtcNow,
                WorkCalendarId   = created.Id,
            });
            await db.SaveChangesAsync();
        }

        var delete = await _client.DeleteAsync($"/api/calendars/{created.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadAsync<object>(delete)).Message.Should().Contain("used by 1 workspace");

        (await _client.GetAsync($"/api/calendars/{created.Id}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "the calendar must survive the refused delete");

        var usage = await _client.GetAsync($"/api/calendars/{created.Id}/usage");
        usage.StatusCode.Should().Be(HttpStatusCode.OK);
        var usageBody = (await ReadAsync<WorkCalendarUsageDto>(usage)).Data!;
        usageBody.TotalWorkspaceCount.Should().Be(1);
        usageBody.Workspaces.Should().ContainSingle(w => w.Id == workspaceId);
    }

    [Fact]
    public async Task SetDefault_MovesTheDefault_AndTheDefaultCannotBeDeleted()
    {
        await SignInAsAdminAsync();
        var previousDefault = (await ListCalendarsAsync()).Single(c => c.IsDefault);
        var created = await CreateCalendarAsync(UniqueName("New default"));

        try
        {
            var setDefault = await _client.PutAsync($"/api/calendars/{created.Id}/default", null);
            setDefault.StatusCode.Should().Be(HttpStatusCode.OK);

            var calendars = await ListCalendarsAsync();
            calendars.Where(c => c.IsDefault).Select(c => c.Id).Should().Equal(new[] { created.Id },
                "there is exactly one default at all times");

            (await _client.DeleteAsync($"/api/calendars/{created.Id}"))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "the default calendar cannot be deleted");
        }
        finally
        {
            // Hand the default back so the other tests in this class see the seeded state.
            await _client.PutAsync($"/api/calendars/{previousDefault.Id}/default", null);
        }
    }
}
