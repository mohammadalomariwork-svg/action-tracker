using System.Net;
using System.Net.Http.Headers;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.ProjectPeople.DTOs;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 P-2 through the real pipeline (policies, filters, controllers):
/// project people of a project in a private workspace see that project and what
/// is under it, and nothing else of the workspace; their writes are limited by
/// their role; removal ends access at once; dashboards and reports follow; a user
/// with no link gets what they got before.
/// </summary>
public class ProjectPeopleAccessIntegrationTests
    : ProjectPeopleIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    public ProjectPeopleAccessIntegrationTests(ActionTrackerWebApplicationFactory factory) : base(factory) { }

    private static Session Linked(World w, string link) => link switch
    {
        "viewer"  => w.Viewer,
        "sponsor" => w.Sponsor,
        "team"    => w.Team,
        _         => throw new ArgumentOutOfRangeException(nameof(link)),
    };

    // ── Reads ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("viewer", AppRoles.TeamMember)]
    [InlineData("sponsor", AppRoles.TeamMember)]
    [InlineData("team", AppRoles.TeamMember)]
    [InlineData("viewer", AppRoles.Admin)]
    [InlineData("sponsor", AppRoles.Admin)]
    [InlineData("team", AppRoles.Admin)]
    public async Task A_project_person_sees_that_project_and_its_items_and_nothing_else_of_the_private_workspace(string link, string systemRole)
    {
        var w = await BuildWorldAsync(systemRole);
        var me = Linked(w, link);

        // The project and everything under it.
        var project = await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", me);
        project.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadAsync<ProjectResponseDto>(project);
        dto.MyRoles.Should().Contain(link == "team" ? "teamMember" : link);
        dto.CanManage.Should().BeFalse();
        dto.CanOpenWorkspace.Should().BeFalse();
        dto.WorkspaceTitle.Should().NotBeEmpty("the project shows its workspace's name (decision D7)");

        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/stats", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/risks/{w.RiskId}", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.ItemOtherId}", me)).Should().Be(HttpStatusCode.OK);
        var subs = await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}/sub-items", me);
        subs.StatusCode.Should().Be(HttpStatusCode.OK);
        (await subs.Content.ReadAsStringAsync()).Should().Contain(w.SubTeamId.ToString());
        var updates = await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}/updates", me);
        updates.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updates.Content.ReadAsStringAsync()).Should().Contain(w.UpdateId.ToString());
        (await StatusAsync(HttpMethod.Get, $"/api/documents?entityType=ActionItem&entityId={w.ItemTeamId}", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/documents/{w.ItemDocId}/download", me)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/documents/{w.ProjectDocId}/download", me)).Should().Be(HttpStatusCode.OK);
        var comments = await SendAsync(HttpMethod.Get, $"/api/comments?entityType=Project&entityId={w.ProjectId}", me);
        comments.StatusCode.Should().Be(HttpStatusCode.OK);
        (await comments.Content.ReadAsStringAsync()).Should().Contain(w.CommentId.ToString());
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/people", me)).Should().Be(HttpStatusCode.OK);

        // The item's own tag (through the item), never the workspace's suggestions.
        var item = await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}", me));
        item.Tags.Select(t => t.Name).Should().Contain(w.Tag);
        item.ViaProjectRole.Should().BeTrue();

        // In the lists, and in My Projects with the role.
        var list = await SendAsync(HttpMethod.Get, "/api/projects?pageSize=500", me);
        var listText = await list.Content.ReadAsStringAsync();
        listText.Should().Contain(w.ProjectId.ToString()).And.NotContain(w.SiblingId.ToString());
        var mine = await ReadAsync<PagedResult<ProjectResponseDto>>(await SendAsync(HttpMethod.Get, "/api/projects?mine=true&pageSize=500", me));
        mine.Items.Select(p => p.Id).Should().Contain(w.ProjectId).And.NotContain(w.SiblingId);
        var items = await (await SendAsync(HttpMethod.Get, "/api/action-items?pageSize=500", me)).Content.ReadAsStringAsync();
        items.Should().Contain(w.ItemTeamId.ToString()).And.NotContain(w.SiblingItemId.ToString());

        // Nothing else of the workspace: sibling project and all under it.
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.SiblingId}", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.SiblingId}/milestones/{w.SiblingMilestoneId}", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.SiblingId}/risks/{w.SiblingRiskId}", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.SiblingItemId}", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.SiblingItemId}/sub-items", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.SiblingItemId}/updates", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/documents/{w.SiblingDocId}/download", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/comments?entityType=Project&entityId={w.SiblingId}", me)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.SiblingId}/people", me)).Should().Be(HttpStatusCode.NotFound);

        // ...nor the workspace itself, its member lists or settings. A route whose
        // policy the person lacks answers 403 before the workspace is looked at.
        var expected = systemRole == AppRoles.Admin
            ? new[] { HttpStatusCode.NotFound }
            : new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden };
        foreach (var url in WorkspaceUrls(w.WorkspaceId))
            (await StatusAsync(HttpMethod.Get, url, me)).Should().BeOneOf(expected, url);
        foreach (var url in new[] { $"/api/workspaces/{w.WorkspaceId}/assignable-users", $"/api/workspaces/{w.WorkspaceId}/project-managers", $"/api/workspaces/{w.WorkspaceId}/calendar" })
            (await StatusAsync(HttpMethod.Get, url, me)).Should().Be(HttpStatusCode.NotFound, url);
        var workspaces = await SendAsync(HttpMethod.Get, "/api/workspaces", me);
        if (workspaces.StatusCode == HttpStatusCode.OK)
            (await workspaces.Content.ReadAsStringAsync()).Should().NotContain(w.WorkspaceId.ToString());
        (await (await SendAsync(HttpMethod.Get, "/api/workspaces/summary", me)).Content.ReadAsStringAsync())
            .Should().NotContain(w.WorkspaceId.ToString());
    }

    private static IEnumerable<string> WorkspaceUrls(Guid ws) => new[]
    {
        $"/api/workspaces/{ws}", $"/api/workspaces/{ws}/stats", $"/api/workspaces/{ws}/members",
        $"/api/workspaces/{ws}/strategic-project-approvers", $"/api/workspaces/{ws}/tags",
        $"/api/workspaces/{ws}/project-classifications", $"/api/projects/strategic-objectives-for-workspace/{ws}",
    };

    [Fact]
    public async Task The_project_calendar_and_assignee_picker_never_list_the_workspaces_people()
    {
        var w = await BuildWorldAsync();
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/calendar", w.Viewer)).Should().Be(HttpStatusCode.OK);

        // Project-only PM: the project's own people (decision D4).
        var pool = await ReadAsync<List<AssignableUserDto>>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/assignable-users", w.AdditionalPm));
        pool.Select(u => u.UserId).Should().Contain(new[] { w.Lead.UserId, w.AdditionalPm.UserId, w.Team.UserId })
            .And.NotContain(w.WsAdmin.UserId);

        // Workspace admin: the workspace's pool plus the team.
        var full = await ReadAsync<List<AssignableUserDto>>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/assignable-users", w.WsAdmin));
        full.Select(u => u.UserId).Should().Contain(new[] { w.WsAdmin.UserId, w.Team.UserId });
    }

    // ── Writes by role ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_team_member_cannot_change_project_fields_milestones_risks_or_other_peoples_items()
    {
        // Project Manager as system role: every policy passes, so each 403 below is the project-role ceiling.
        var w = await BuildWorldAsync(AppRoles.ProjectManager);
        var t = w.Team;

        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", t, ProjectPut(w, "Renamed"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/milestones", t, MilestoneBody("New"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", t, MilestoneBody("Changed"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", t)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/risks", t, new { projectId = w.ProjectId, title = "R2", description = "d", category = "c", probabilityScore = 1, impactScore = 1 })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", t, new { title = "Mine now" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemOtherId}/status", t, new { status = "inProgress" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{w.ItemOtherId}/sub-items", t, new { title = "S", endDate = "2030-06-28", assigneeIds = Array.Empty<string>() })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}/done", t, new { isDone = true })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{w.ItemOtherId}/updates", t, new { content = "x", submit = true })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={w.ProjectId}", t, new { content = "x" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/documents/{w.ProjectDocId}", t)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, "/api/action-items", t, new { title = "New", workspaceId = w.WorkspaceId, projectId = w.ProjectId, assigneeIds = new[] { t.UserId }, priority = "medium", dueDate = End })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", t, new { userId = w.Outsider.UserId, role = "viewer" })).Should().Be(HttpStatusCode.Forbidden);

        // ...but works on what is assigned to them.
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", t, new { title = "Team item, updated" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemTeamId}/status", t, new { status = "inProgress" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemTeamId}/sub-items/{w.SubTeamId}/done", t, new { isDone = true })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{w.ItemTeamId}/updates", t, new { content = "Done my part", submit = true })).Should().Be(HttpStatusCode.Created);

        // Through the role, an item stays where it is and its tags are read-only (D5).
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", t, new { projectId = Guid.Empty })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", t, new { tags = new[] { "x" } })).Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("viewer")]
    [InlineData("sponsor")]
    public async Task Viewers_and_sponsors_are_read_only(string link)
    {
        var w = await BuildWorldAsync(AppRoles.ProjectManager);
        var me = Linked(w, link);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", me, ProjectPut(w, "Renamed"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", me, new { title = "x" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemTeamId}/sub-items/{w.SubTeamId}/done", me, new { isDone = true })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{w.ItemTeamId}/updates", me, new { content = "x" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={w.ProjectId}", me, new { content = "x" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, "/api/action-items/workflow/date-change-request", me, new { actionItemId = w.ItemTeamId, newDueDate = End, reason = "x" })).Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_additional_pm_has_the_lead_pms_rights_but_adds_no_project_managers()
    {
        var w = await BuildWorldAsync();
        var pm = w.AdditionalPm;

        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", pm, ProjectPut(w, "Renamed by additional PM"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/milestones", pm, MilestoneBody("Added"))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", pm, MilestoneBody("Changed"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", pm, new { title = "Edited by PM" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, "/api/action-items", pm, new { title = "From PM", workspaceId = w.WorkspaceId, projectId = w.ProjectId, assigneeIds = new[] { w.Team.UserId }, priority = "medium", dueDate = End })).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", pm, new { userId = w.Outsider.UserId, role = "viewer" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/viewer/{w.Outsider.UserId}", pm)).Should().Be(HttpStatusCode.OK);

        // Decision D4: adding project managers or changing the lead needs the
        // workspace's Project Managers list, which they do not see.
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", pm, new { userId = w.WsAdmin.UserId, role = "projectManager" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", pm, ProjectPut(w, "x", lead: pm.UserId))).Should().Be(HttpStatusCode.Forbidden);

        // A creation in another private workspace through the role is refused (D12).
        (await StatusAsync(HttpMethod.Post, "/api/action-items", pm, new { title = "Stray", workspaceId = w.WorkspaceId, assigneeIds = new[] { pm.UserId }, priority = "medium", dueDate = End })).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_additional_pm_may_submit_and_the_request_still_routes_from_the_lead_pm()
    {
        var w = await BuildWorldAsync();
        var approver = await SignInAsync("p2.approver." + Guid.NewGuid().ToString("N")[..6], AppRoles.Admin);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == w.ProjectId);
            project.ProjectType = ProjectType.Strategic;
            db.WorkspaceStrategicProjectApprovers.Add(new WorkspaceStrategicProjectApprover
                { WorkspaceId = w.WorkspaceId, UserId = approver.UserId, UserName = approver.UserId, Order = 1 });
            // As today, an approver of a private workspace must be one of its members to see the request.
            db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = w.WorkspaceId, UserId = approver.UserId, UserName = approver.UserId });
            // Submit needs a milestone in every phase, each with an action item.
            foreach (var phase in Enum.GetValues<ProjectPhase>().Where(p => p != ProjectPhase.Initiation))
            {
                var m = NewMilestone(w.ProjectId, phase.ToString(), phase);
                db.Milestones.Add(m);
                db.ActionItems.Add(NewItem(w.WorkspaceId, w.ProjectId, m.Id, "Phase " + phase, w.Team.UserId));
            }
            await db.SaveChangesAsync();
        }

        var submit = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", w.AdditionalPm, new { projectId = w.ProjectId, reason = "Ready" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.ProjectApprovalRequests.SingleAsync(r => r.ProjectId == w.ProjectId);
            request.AssignedReviewerUserId.Should().Be(approver.UserId, "the workspace chain is unchanged");
            request.RequestedByUserId.Should().Be(w.AdditionalPm.UserId);
            var review = await SendAsync(HttpMethod.Put, $"/api/projects/workflow/requests/{request.Id}/review", approver,
                new { requestId = request.Id, isApproved = true, reviewComment = "OK" });
            review.StatusCode.Should().Be(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
        }

        // The decision goes to the lead PM, as before — not to the additional PM.
        var leadEmail = $"a5.p2.lead.";
        var reviewed = await Factory.Emails.WaitForAsync(e => e.TemplateKey == "ProjectApproval.Reviewed" && e.RelatedEntityId == w.ProjectId);
        reviewed.Should().NotBeEmpty();
        reviewed.SelectMany(e => e.Recipients).Should().OnlyContain(r => r.StartsWith(leadEmail));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notified = await db.AppNotifications.Where(n => n.RelatedEntityId == w.ProjectId && n.UserId == w.Lead.UserId).CountAsync();
            notified.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Removing_a_person_removes_their_access_on_the_next_request()
    {
        var w = await BuildWorldAsync();
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", w.Viewer)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", w.Team)).Should().Be(HttpStatusCode.OK);

        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/viewer/{w.Viewer.UserId}", w.WsAdmin)).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/teamMember/{w.Team.UserId}", w.WsAdmin)).Should().Be(HttpStatusCode.OK);

        foreach (var who in new[] { w.Viewer, w.Team })
        {
            (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", who)).Should().Be(HttpStatusCode.NotFound);
            (await StatusAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}", who)).Should().Be(HttpStatusCode.NotFound);
            (await StatusAsync(HttpMethod.Get, $"/api/documents/{w.ItemDocId}/download", who)).Should().Be(HttpStatusCode.NotFound);
            (await (await SendAsync(HttpMethod.Get, "/api/projects?mine=true", who)).Content.ReadAsStringAsync()).Should().NotContain(w.ProjectId.ToString());
        }
    }

    [Fact]
    public async Task Adding_a_person_notifies_them_and_never_the_person_who_added_them()
    {
        var w = await BuildWorldAsync();
        var added = await SignInAsync("p2.added." + Guid.NewGuid().ToString("N")[..6], AppRoles.TeamMember);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var project = await db.Projects.IgnoreQueryFilters().FirstAsync(p => p.Id == w.ProjectId);
            project.Name = "<b>Bold</b> & co";
            await db.SaveChangesAsync();
        }

        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", w.WsAdmin, new { userId = added.UserId, role = "teamMember" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", w.WsAdmin, new { userId = w.WsAdmin.UserId, role = "viewer" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", w.WsAdmin, new { userId = added.UserId, role = "teamMember" })).Should().Be(HttpStatusCode.Conflict);

        var mails = await Factory.Emails.WaitForAsync(e => e.TemplateKey == "Project.PersonAdded" && e.RelatedEntityId == w.ProjectId);
        mails.Should().ContainSingle();
        mails[0].Recipients.Should().ContainSingle().Which.Should().StartWith("a5.p2.added.");
        mails[0].Placeholders["ProjectName"].Should().Be("&lt;b&gt;Bold&lt;/b&gt; &amp; co");
        mails[0].Placeholders["RoleName"].Should().Be("team member");
        using var scope2 = Factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db2.AppNotifications.CountAsync(n => n.RelatedEntityId == w.ProjectId && n.ActionType == "PersonAdded" && n.UserId == added.UserId)).Should().Be(1);
        (await db2.AppNotifications.CountAsync(n => n.RelatedEntityId == w.ProjectId && n.ActionType == "PersonAdded" && n.UserId == w.WsAdmin.UserId)).Should().Be(0);
    }

    [Fact]
    public async Task People_rules_are_enforced()
    {
        var w = await BuildWorldAsync();
        var admin = w.WsAdmin;
        // An additional PM must be on the workspace's Project Managers list; the lead is not also an additional PM.
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", admin, new { userId = w.Viewer.UserId, role = "projectManager" })).Should().Be(HttpStatusCode.BadRequest);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", admin, new { userId = w.Lead.UserId, role = "projectManager" })).Should().Be(HttpStatusCode.BadRequest);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", admin, new { userId = w.WsAdmin.UserId, role = "projectManager" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", admin, new { userId = w.Viewer.UserId, role = "owner" })).Should().Be(HttpStatusCode.BadRequest);
        // The last sponsor stays.
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/sponsor/{w.Sponsor.UserId}", admin)).Should().Be(HttpStatusCode.BadRequest);
        // Sponsors, viewers and team members can be any active user, also outside the workspace.
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", admin, new { userId = w.Outsider.UserId, role = "sponsor" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/sponsor/{w.Sponsor.UserId}", admin)).Should().Be(HttpStatusCode.OK);
        var lookup = await SendAsync(HttpMethod.Get, "/api/projects/people-lookup?search=p2.outsider", admin);
        lookup.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Dashboards, reports, exports ────────────────────────────────────────

    [Fact]
    public async Task Dashboards_reports_and_lists_contain_only_the_permitted_project()
    {
        var w = await BuildWorldAsync();
        var viewer = w.Viewer;   // Team Member: Dashboard.View

        var projects = await (await SendAsync(HttpMethod.Get, "/api/dashboard/projects", viewer)).Content.ReadAsStringAsync();
        projects.Should().Contain(w.ProjectId.ToString()).And.NotContain(w.SiblingId.ToString()).And.NotContain(w.SiblingTitle);
        var actions = await SendAsync(HttpMethod.Get, "/api/dashboard/action-items", viewer);
        actions.StatusCode.Should().Be(HttpStatusCode.OK);
        (await actions.Content.ReadAsStringAsync()).Should().NotContain(w.SiblingTitle);
        var drill = await (await SendAsync(HttpMethod.Get, "/api/action-items?openOnly=true&pageSize=500", viewer)).Content.ReadAsStringAsync();
        drill.Should().Contain(w.ItemTeamId.ToString()).And.NotContain(w.SiblingItemId.ToString());
        foreach (var url in new[] { "/api/dashboard/kpis", "/api/dashboard/management", "/api/dashboard/team-activity", "/api/dashboard/status-breakdown", "/api/dashboard/team-workload" })
            (await (await SendAsync(HttpMethod.Get, url, viewer)).Content.ReadAsStringAsync()).Should().NotContain(w.SiblingTitle, url);

        // Reports (Admin system role: Reports.View / Export) — privileged, still only the permitted project.
        var adminViewer = await SignInAsync("p2.adminviewer." + Guid.NewGuid().ToString("N")[..6], AppRoles.Admin);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/people", w.WsAdmin, new { userId = adminViewer.UserId, role = "viewer" })).Should().Be(HttpStatusCode.OK);
        var csv = await (await SendAsync(HttpMethod.Get, "/api/reports/export-csv", adminViewer)).Content.ReadAsStringAsync();
        csv.Should().Contain("Team item").And.NotContain(w.SiblingTitle);
        (await (await SendAsync(HttpMethod.Get, "/api/reports/summary", adminViewer)).Content.ReadAsStringAsync()).Should().NotContain(w.SiblingTitle);
        (await (await SendAsync(HttpMethod.Get, "/api/workspaces", adminViewer)).Content.ReadAsStringAsync()).Should().NotContain(w.WorkspaceId.ToString());
        (await (await SendAsync(HttpMethod.Get, "/api/dashboard/projects", adminViewer)).Content.ReadAsStringAsync())
            .Should().Contain(w.ProjectId.ToString()).And.NotContain(w.SiblingId.ToString());
    }

    // ── No link: as before ──────────────────────────────────────────────────

    [Fact]
    public async Task A_user_with_no_link_gets_what_they_got_before()
    {
        var priv = await BuildWorldAsync();
        var pub  = await BuildWorldAsync(isPrivate: false);
        var outsider = priv.Outsider;   // Project Manager role, no link to either world

        // Private workspace: nothing, as before.
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{priv.ProjectId}", outsider)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/action-items/{priv.ItemTeamId}", outsider)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Get, $"/api/documents/{priv.ProjectDocId}/download", outsider)).Should().Be(HttpStatusCode.NotFound);
        (await (await SendAsync(HttpMethod.Get, "/api/projects?pageSize=500", outsider)).Content.ReadAsStringAsync()).Should().NotContain(priv.ProjectId.ToString());

        // Public workspace: by id as before (read and write), not in the lists (resolver), as before.
        var p = await SendAsync(HttpMethod.Get, $"/api/projects/{pub.ProjectId}", outsider);
        p.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await ReadAsync<ProjectResponseDto>(p);
        dto.MyRoles.Should().BeEmpty();
        dto.CanManage.Should().BeTrue();
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{pub.ItemOtherId}", outsider, new { title = "Public edit" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{pub.ProjectId}/milestones/{pub.MilestoneId}", outsider, MilestoneBody("Public"))).Should().Be(HttpStatusCode.OK);
        (await (await SendAsync(HttpMethod.Get, "/api/projects?pageSize=500", outsider)).Content.ReadAsStringAsync()).Should().NotContain(pub.ProjectId.ToString());
        (await StatusAsync(HttpMethod.Get, $"/api/workspaces/{pub.WorkspaceId}/assignable-users", outsider)).Should().Be(HttpStatusCode.OK);
    }

    // ── Gaps closed with P-2 (decision D12) ─────────────────────────────────

    [Fact]
    public async Task Private_items_and_risks_can_no_longer_be_reached_by_id_through_requests_or_restore()
    {
        var w = await BuildWorldAsync();
        var outsider = w.Outsider;   // Project Manager role: ActionItems.Edit, Projects.Edit
        (await StatusAsync(HttpMethod.Post, "/api/action-items/workflow/date-change-request", outsider, new { actionItemId = w.ItemTeamId, newDueDate = End, reason = "x" })).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Post, "/api/action-items/workflow/status-change-request", outsider, new { actionItemId = w.ItemTeamId, newStatus = "done", reason = "x" })).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Patch, $"/api/projects/{w.ProjectId}/risks/{w.RiskId}/restore", outsider)).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Post, "/api/projects", outsider, new
        {
            name = "Into a private workspace", workspaceId = w.WorkspaceId, projectType = "operational", priority = "medium",
            projectManagerUserId = w.Lead.UserId, sponsorUserIds = new[] { outsider.UserId }, plannedStartDate = Start, plannedEndDate = End,
        })).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_new_endpoints_are_in_swagger_with_their_policies_and_need_a_signed_in_user()
    {
        var w = await BuildWorldAsync();
        foreach (var (method, url) in new[]
        {
            (HttpMethod.Get, $"/api/projects/{w.ProjectId}/people"), (HttpMethod.Post, $"/api/projects/{w.ProjectId}/people"),
            (HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/viewer/x"), (HttpMethod.Get, "/api/projects/people-lookup"),
            (HttpMethod.Get, $"/api/projects/{w.ProjectId}/assignable-users"), (HttpMethod.Get, $"/api/projects/{w.ProjectId}/calendar"),
        })
            (await StatusAsync(method, url, null!)).Should().Be(HttpStatusCode.Unauthorized, url);

        // Without the permission: 403 (Viewer role holds no Projects permission in the seed).
        var none = await SignInAsync("p2.noperm." + Guid.NewGuid().ToString("N")[..6], AppRoles.Viewer);
        (await StatusAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}/people", none)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Get, "/api/projects", none)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Get, "/api/projects/people-lookup", w.Viewer)).Should().Be(HttpStatusCode.Forbidden);

        var swagger = await (await Client.GetAsync("/swagger/v1/swagger.json")).Content.ReadAsStringAsync();
        swagger.Should().Contain("/api/projects/{projectId}/people").And.Contain("/api/projects/people-lookup")
            .And.Contain("/api/projects/{projectId}/assignable-users").And.Contain("/api/projects/{projectId}/calendar");
    }
}
