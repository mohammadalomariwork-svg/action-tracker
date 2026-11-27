using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ActionTracker.Application.Features.ActionItems;
using ActionTracker.Application.Features.ActionItems.DTOs;
using ActionTracker.Application.Features.Documents.DTOs;
using ActionTracker.Application.Features.ProjectPeople;
using ActionTracker.Application.Features.Projects;
using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 P-2b through the real pipeline: in a public workspace, writes follow
/// the caller's link to the record. Everyone with a system role that passes every
/// permission policy (Project Manager or Admin), so each 403 here is the ownership
/// rule, not a policy. Approved rights table: B1–B6.
/// </summary>
public class OwnershipRuleIntegrationTests
    : ProjectPeopleIntegrationTestBase, IClassFixture<ActionTrackerWebApplicationFactory>
{
    public OwnershipRuleIntegrationTests(ActionTrackerWebApplicationFactory factory) : base(factory) { }

    /// <summary>The P-2 world in a public workspace, plus workspace members and a standalone item.</summary>
    private sealed class Pub
    {
        public World W = null!;
        public Session Member = null!;       // plain workspace member, no link
        public Session Creator = null!;      // member who created the standalone item
        public Session Assignee = null!;     // member assigned to the standalone item
        public Session SubAssignee = null!;  // member assigned only to the standalone item's sub-item
        public Session AdminRole = null!;    // Admin system role, no link
        public Session PmoHead = null!;      // PMO Head system role, no link
        public Guid StandaloneId, StandaloneSubId, StandaloneDocId, MilestoneDocId, RiskDocId, MilestoneCommentId;
    }

    private async Task<Pub> BuildPublicAsync()
    {
        var w = await BuildWorldAsync(AppRoles.ProjectManager, isPrivate: false);
        var n = Guid.NewGuid().ToString("N")[..8];
        var p = new Pub
        {
            W           = w,
            Member      = await SignInAsync($"p2b.member.{n}", AppRoles.ProjectManager),
            Creator     = await SignInAsync($"p2b.creator.{n}", AppRoles.ProjectManager),
            Assignee    = await SignInAsync($"p2b.assignee.{n}", AppRoles.ProjectManager),
            SubAssignee = await SignInAsync($"p2b.subassignee.{n}", AppRoles.ProjectManager),
            AdminRole   = await SignInAsync($"p2b.admin.{n}", AppRoles.Admin),
            PmoHead     = await SignInAsync($"p2b.pmo.{n}", AppRoles.PmoHead),
        };
        await AddRoleAsync(p.PmoHead, AppRoles.ProjectManager);   // every policy passes, so 403s are the rule

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var m in new[] { p.Member, p.Creator, p.Assignee, p.SubAssignee })
            db.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = w.WorkspaceId, UserId = m.UserId, UserName = m.UserId });

        var s = NewItem(w.WorkspaceId, null, null, "Standalone " + n, p.Assignee.UserId);
        s.CreatedByUserId = p.Creator.UserId;
        db.ActionItems.Add(s);
        var sub = NewSub(s.Id, "Standalone sub", p.SubAssignee.UserId);
        db.ActionItemSubItems.Add(sub);
        (p.StandaloneId, p.StandaloneSubId) = (s.Id, sub.Id);
        p.StandaloneDocId = AddDoc(db, "ActionItem", s.Id, p.Creator.UserId);
        p.MilestoneDocId  = AddDoc(db, "Milestone", w.MilestoneId, w.WsAdmin.UserId);
        p.RiskDocId       = AddDoc(db, "ProjectRisk", w.RiskId, w.WsAdmin.UserId);
        var mc = new Comment { Id = Guid.NewGuid(), RelatedEntityType = "Milestone", RelatedEntityId = w.MilestoneId, Content = "M note", AuthorUserId = w.WsAdmin.UserId };
        db.Comments.Add(mc);
        p.MilestoneCommentId = mc.Id;
        await db.SaveChangesAsync();
        return p;
    }

    private Session Persona(Pub p, string who) => who switch
    {
        "outsider"    => p.W.Outsider,
        "member"      => p.Member,
        "viewer"      => p.W.Viewer,
        "sponsor"     => p.W.Sponsor,
        "team"        => p.W.Team,
        "lead"        => p.W.Lead,
        "addpm"       => p.W.AdditionalPm,
        "wsadmin"     => p.W.WsAdmin,
        "adminrole"   => p.AdminRole,
        "pmohead"     => p.PmoHead,
        "creator"     => p.Creator,
        "assignee"    => p.Assignee,
        "subassignee" => p.SubAssignee,
        _ => throw new ArgumentOutOfRangeException(nameof(who)),
    };

    private async Task<HttpResponseMessage> UploadAsync(Session as_, string type, Guid id)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(type), "entityType" },
            { new StringContent(id.ToString()), "entityId" },
            { new StringContent("Evidence"), "name" },
        };
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 P-2b"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "evidence.pdf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", as_.Token);
        return await Client.SendAsync(request);
    }

    private static object RiskBody(Guid projectId, string title) => new
    {
        projectId, title, description = "d", category = "c", probabilityScore = 2, impactScore = 2, status = "Open",
    };

    private static object ItemBody(Guid workspaceId, Guid? projectId, string assignee, string title = "New") => new
    {
        title, workspaceId, projectId, assigneeIds = new[] { assignee }, priority = "medium", dueDate = End,
    };

    /// <summary>Every kind of write on the project, its milestone / risk / items / files / comments and on the standalone item.</summary>
    private IEnumerable<(string What, Func<Session, Task<HttpResponseMessage>> Call)> Writes(Pub p)
    {
        var w = p.W;
        var pid = w.ProjectId;
        yield return ("project PUT", s => SendAsync(HttpMethod.Put, $"/api/projects/{pid}", s, ProjectPut(w, "Changed by " + s.UserId)));
        yield return ("project DELETE", s => SendAsync(HttpMethod.Delete, $"/api/projects/{pid}", s));
        yield return ("project restore", s => SendAsync(HttpMethod.Patch, $"/api/projects/{pid}/restore", s));
        yield return ("people POST", s => SendAsync(HttpMethod.Post, $"/api/projects/{pid}/people", s, new { userId = p.Member.UserId, role = "viewer" }));
        yield return ("people DELETE", s => SendAsync(HttpMethod.Delete, $"/api/projects/{pid}/people/teamMember/{w.Team.UserId}", s));
        yield return ("milestone POST", s => SendAsync(HttpMethod.Post, $"/api/projects/{pid}/milestones", s, MilestoneBody("New")));
        yield return ("milestone PUT", s => SendAsync(HttpMethod.Put, $"/api/projects/{pid}/milestones/{w.MilestoneId}", s, MilestoneBody("Changed")));
        yield return ("milestone DELETE", s => SendAsync(HttpMethod.Delete, $"/api/projects/{pid}/milestones/{w.MilestoneId}", s));
        yield return ("milestone baseline", s => SendAsync(HttpMethod.Post, $"/api/projects/{pid}/milestones/baseline", s));
        yield return ("risk POST", s => SendAsync(HttpMethod.Post, $"/api/projects/{pid}/risks", s, RiskBody(pid, "New risk")));
        yield return ("risk PUT", s => SendAsync(HttpMethod.Put, $"/api/projects/{pid}/risks/{w.RiskId}", s, RiskBody(pid, "Changed risk")));
        yield return ("risk DELETE", s => SendAsync(HttpMethod.Delete, $"/api/projects/{pid}/risks/{w.RiskId}", s));
        yield return ("project item POST", s => SendAsync(HttpMethod.Post, "/api/action-items", s, ItemBody(w.WorkspaceId, pid, w.Team.UserId)));
        yield return ("item PUT", s => SendAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", s, new { title = "Changed" }));
        yield return ("item status", s => SendAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemOtherId}/status", s, new { status = "inProgress" }));
        yield return ("item DELETE", s => SendAsync(HttpMethod.Delete, $"/api/action-items/{w.ItemOtherId}", s));
        yield return ("sub-item POST", s => SendAsync(HttpMethod.Post, $"/api/action-items/{w.ItemOtherId}/sub-items", s, new { title = "S", endDate = End, assigneeIds = Array.Empty<string>() }));
        yield return ("sub-item PUT", s => SendAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}", s, new { title = "S2", endDate = End, assigneeIds = Array.Empty<string>() }));
        yield return ("sub-item done", s => SendAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}/done", s, new { isDone = true }));
        yield return ("sub-item DELETE", s => SendAsync(HttpMethod.Delete, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}", s));
        yield return ("update POST", s => SendAsync(HttpMethod.Post, $"/api/action-items/{w.ItemOtherId}/updates", s, new { content = "x", submit = true }));
        yield return ("sub-item update POST", s => SendAsync(HttpMethod.Post, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}/updates", s, new { content = "x", submit = true }));
        yield return ("date request", s => SendAsync(HttpMethod.Post, "/api/action-items/workflow/date-change-request", s, new { actionItemId = w.ItemOtherId, newDueDate = End, reason = "x" }));
        yield return ("status request", s => SendAsync(HttpMethod.Post, "/api/action-items/workflow/status-change-request", s, new { actionItemId = w.ItemOtherId, newStatus = "done", reason = "x" }));
        yield return ("escalate", s => SendAsync(HttpMethod.Post, "/api/action-items/workflow/escalate", s, new { actionItemId = w.ItemOtherId, reason = "x" }));
        yield return ("give direction", s => SendAsync(HttpMethod.Post, "/api/action-items/workflow/give-direction", s, new { actionItemId = w.ItemOtherId, directionText = "x" }));
        yield return ("comment on project", s => SendAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={pid}", s, new { content = "x" }));
        yield return ("comment on milestone", s => SendAsync(HttpMethod.Post, $"/api/comments?entityType=Milestone&entityId={w.MilestoneId}", s, new { content = "x" }));
        yield return ("comment on risk", s => SendAsync(HttpMethod.Post, $"/api/comments?entityType=ProjectRisk&entityId={w.RiskId}", s, new { content = "x" }));
        yield return ("file on project", s => UploadAsync(s, "Project", pid));
        yield return ("file on milestone", s => UploadAsync(s, "Milestone", w.MilestoneId));
        yield return ("file on risk", s => UploadAsync(s, "ProjectRisk", w.RiskId));
        yield return ("file on item", s => UploadAsync(s, "ActionItem", w.ItemOtherId));
        yield return ("file on sub-item", s => UploadAsync(s, "ActionItemSubItem", w.SubOtherId));
        yield return ("project file DELETE", s => SendAsync(HttpMethod.Delete, $"/api/documents/{w.ProjectDocId}", s));
        yield return ("milestone file DELETE", s => SendAsync(HttpMethod.Delete, $"/api/documents/{p.MilestoneDocId}", s));
        yield return ("risk file DELETE", s => SendAsync(HttpMethod.Delete, $"/api/documents/{p.RiskDocId}", s));
        yield return ("item file DELETE", s => SendAsync(HttpMethod.Delete, $"/api/documents/{w.ItemDocId}", s));
        yield return ("standalone PUT", s => SendAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", s, new { title = "Changed" }));
        yield return ("standalone status", s => SendAsync(HttpMethod.Patch, $"/api/action-items/{p.StandaloneId}/status", s, new { status = "inProgress" }));
        yield return ("standalone DELETE", s => SendAsync(HttpMethod.Delete, $"/api/action-items/{p.StandaloneId}", s));
        yield return ("standalone sub-item done", s => SendAsync(HttpMethod.Patch, $"/api/action-items/{p.StandaloneId}/sub-items/{p.StandaloneSubId}/done", s, new { isDone = true }));
        yield return ("standalone update POST", s => SendAsync(HttpMethod.Post, $"/api/action-items/{p.StandaloneId}/updates", s, new { content = "x", submit = true }));
        yield return ("standalone file", s => UploadAsync(s, "ActionItem", p.StandaloneId));
        yield return ("standalone file DELETE", s => SendAsync(HttpMethod.Delete, $"/api/documents/{p.StandaloneDocId}", s));
    }

    // ── No link: read only ──────────────────────────────────────────────────

    [Theory]
    [InlineData("outsider")]   // not a member of the workspace
    [InlineData("member")]     // plain member of the workspace
    [InlineData("viewer")]
    [InlineData("sponsor")]
    public async Task In_a_public_workspace_a_reader_cannot_change_any_record_by_id_and_still_reads_it(string who)
    {
        var p = await BuildPublicAsync();
        var me = Persona(p, who);

        var notRefused = new List<string>();
        foreach (var (what, call) in Writes(p))
        {
            var response = await call(me);
            if (response.StatusCode != HttpStatusCode.Forbidden)
                notRefused.Add($"{what} → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            else
                (await MessageAsync(response)).Should().NotBeNullOrWhiteSpace($"{what}: a 403 carries its reason");
        }
        notRefused.Should().BeEmpty();

        // Nothing changed.
        var w = p.W;
        (await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", me))).Name.Should().StartWith("P-2 project");
        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemOtherId}", me))).Title.Should().StartWith("Other item");
        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{p.StandaloneId}", me))).Title.Should().StartWith("Standalone");

        // Reads stay.
        foreach (var url in new[]
        {
            $"/api/projects/{w.ProjectId}", $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", $"/api/projects/{w.ProjectId}/risks/{w.RiskId}",
            $"/api/action-items/{w.ItemOtherId}", $"/api/action-items/{w.ItemOtherId}/sub-items", $"/api/action-items/{w.ItemTeamId}/updates",
            $"/api/documents?entityType=Project&entityId={w.ProjectId}", $"/api/documents/{w.ProjectDocId}/download",
            $"/api/comments?entityType=Project&entityId={w.ProjectId}", $"/api/action-items/{p.StandaloneId}", $"/api/projects/{w.ProjectId}/people",
        })
            (await StatusAsync(HttpMethod.Get, url, me)).Should().Be(HttpStatusCode.OK, url);

        // The flags say the same.
        var project = await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", me));
        project.CanManage.Should().BeFalse();
        project.CanSubmit.Should().BeFalse();
        foreach (var id in new[] { w.ItemOtherId, w.ItemTeamId, p.StandaloneId })
        {
            var item = await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{id}", me));
            item.CanWork.Should().BeFalse();
            item.CanDelete.Should().BeFalse();
        }
    }

    [Fact]
    public async Task The_reasons_name_who_may_make_the_change()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        async Task<string?> Reason(HttpMethod m, string url, object? body = null)
            => await MessageAsync(await SendAsync(m, url, p.Member, body));

        (await Reason(HttpMethod.Put, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", MilestoneBody("x"))).Should().Be(ProjectAccessDeniedException.ManagersOnly);
        (await Reason(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", new { title = "x" })).Should().Be(ProjectAccessDeniedException.ItemWorkersOnly);
        (await Reason(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", new { title = "x" })).Should().Be(ProjectAccessDeniedException.StandaloneWorkersOnly);
        (await Reason(HttpMethod.Delete, $"/api/documents/{w.ProjectDocId}")).Should().Be(ProjectAccessDeniedException.FileDeleteOnly);
        (await MessageAsync(await SendAsync(HttpMethod.Delete, $"/api/action-items/{p.StandaloneId}", p.Assignee))).Should().Be(ProjectAccessDeniedException.StandaloneDeleteOnly);
    }

    // ── Team member: own items only ─────────────────────────────────────────

    [Fact]
    public async Task A_team_member_works_only_on_the_items_assigned_to_them()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        var t = w.Team;

        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", t, new { title = "Team item, updated" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemTeamId}/status", t, new { status = "inProgress" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemTeamId}/sub-items/{w.SubTeamId}/done", t, new { isDone = true })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{w.ItemTeamId}/updates", t, new { content = "Done my part", submit = true })).Should().Be(HttpStatusCode.Created);
        (await UploadAsync(t, "ActionItem", w.ItemTeamId)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", t, new { title = "Not mine" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{w.ItemOtherId}/sub-items/{w.SubOtherId}/done", t, new { isDone = true })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", t, ProjectPut(w, "Renamed"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/milestones", t, MilestoneBody("New"))).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{w.ItemTeamId}", t)).Should().Be(HttpStatusCode.Forbidden, "deleting a project's item is a manage right");

        var own = await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemTeamId}", t));
        own.CanWork.Should().BeTrue();
        own.CanDelete.Should().BeFalse();
        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemOtherId}", t))).CanWork.Should().BeFalse();
    }

    // ── Managers ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("lead")]
    [InlineData("addpm")]
    [InlineData("wsadmin")]
    [InlineData("adminrole")]
    [InlineData("pmohead")]
    public async Task The_projects_managers_manage_it(string who)
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        var me = Persona(p, who);

        var project = await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", me));
        project.CanManage.Should().BeTrue();

        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", me, ProjectPut(w, "Renamed by " + who))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/milestones", me, MilestoneBody("Added"))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}/milestones/{w.MilestoneId}", me, MilestoneBody("Changed"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/projects/{w.ProjectId}/risks", me, RiskBody(w.ProjectId, "Added risk"))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", me, new { title = "Edited by a manager" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, "/api/action-items", me, ItemBody(w.WorkspaceId, w.ProjectId, w.Team.UserId))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={w.ProjectId}", me, new { content = "From a manager" })).Should().Be(HttpStatusCode.Created);
        (await UploadAsync(me, "Milestone", w.MilestoneId)).StatusCode.Should().Be(HttpStatusCode.Created);

        // A manager deletes anyone's project / milestone / risk file (B6), and sees that on the list.
        var files = await ReadAsync<List<DocumentResponseDto>>(await SendAsync(HttpMethod.Get, $"/api/documents?entityType=Project&entityId={w.ProjectId}", me));
        files.Should().Contain(d => d.Id == w.ProjectDocId && d.CanDelete);
        (await StatusAsync(HttpMethod.Delete, $"/api/documents/{w.ProjectDocId}", me)).Should().Be(HttpStatusCode.NoContent);

        var item = await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{w.ItemOtherId}", me));
        item.CanWork.Should().BeTrue();
        item.CanDelete.Should().BeTrue("the project is still in Draft");
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{w.ItemOtherId}", me)).Should().Be(HttpStatusCode.NoContent);
    }

    // ── Standalone items ────────────────────────────────────────────────────

    [Theory]
    [InlineData("creator", true)]
    [InlineData("assignee", true)]
    [InlineData("wsadmin", true)]
    [InlineData("adminrole", true)]
    [InlineData("member", false)]
    [InlineData("lead", false)]       // manages a project of the workspace, not this item
    [InlineData("pmohead", false)]    // B1: PMO Head manages projects only
    [InlineData("subassignee", false)]
    public async Task A_standalone_item_is_worked_on_by_its_creator_assignees_workspace_admins_and_the_Admin_role(string who, bool allowed)
    {
        var p = await BuildPublicAsync();
        var me = Persona(p, who);
        var expected = allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden;

        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", me, new { title = "Edited by " + who })).Should().Be(expected);
        (await StatusAsync(HttpMethod.Post, "/api/action-items/workflow/escalate", me, new { actionItemId = p.StandaloneId, reason = "Late" }))
            .Should().Be(expected);
        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{p.StandaloneId}", me))).CanWork.Should().Be(allowed);
    }

    [Fact]
    public async Task A_sub_items_assignee_ticks_it_and_adds_updates_and_files_to_it_only()
    {
        var p = await BuildPublicAsync();
        var me = p.SubAssignee;
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{p.StandaloneId}/sub-items/{p.StandaloneSubId}/done", me, new { isDone = true })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{p.StandaloneId}/sub-items/{p.StandaloneSubId}/updates", me, new { content = "Sub done", submit = true })).Should().Be(HttpStatusCode.Created);
        (await UploadAsync(me, "ActionItemSubItem", p.StandaloneSubId)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}/sub-items/{p.StandaloneSubId}", me, new { title = "Renamed", endDate = End, assigneeIds = new[] { me.UserId } })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Post, $"/api/action-items/{p.StandaloneId}/updates", me, new { content = "On the item", submit = true })).Should().Be(HttpStatusCode.Forbidden);
        (await UploadAsync(me, "ActionItem", p.StandaloneId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_standalone_item_is_deleted_and_restored_by_its_creator_the_workspaces_admins_and_the_Admin_role_not_its_assignees()
    {
        var p = await BuildPublicAsync();
        var id = p.StandaloneId;

        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{id}", p.Assignee))).CanDelete.Should().BeFalse();
        (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{id}", p.Creator))).CanDelete.Should().BeTrue();
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{id}", p.Assignee)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{id}", p.Member)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{id}", p.Creator)).Should().Be(HttpStatusCode.NoContent);

        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{id}/restore", p.Assignee)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{id}/restore", p.W.WsAdmin)).Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{id}", p.AdminRole)).Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(HttpMethod.Patch, $"/api/action-items/{id}/restore", p.Creator)).Should().Be(HttpStatusCode.NoContent);

        // My Actions shows Delete exactly where it would succeed (fix 9).
        var mine = await ReadAsync<PagedResult<ActionItemResponseDto>>(await SendAsync(HttpMethod.Get, "/api/action-items/my-actions?pageSize=100", p.Assignee));
        mine.Items.Should().ContainSingle(i => i.Id == id).Which.CanDelete.Should().BeFalse();
        var created = await ReadAsync<PagedResult<ActionItemResponseDto>>(await SendAsync(HttpMethod.Get, "/api/action-items/created-by-me?pageSize=100", p.Creator));
        created.Items.Should().ContainSingle(i => i.Id == id).Which.CanDelete.Should().BeTrue();
    }

    // ── Creating and moving (B2) ────────────────────────────────────────────

    [Fact]
    public async Task Only_a_workspaces_admins_members_and_the_Admin_PMO_roles_create_in_it()
    {
        var p = await BuildPublicAsync();
        var w = p.W;

        var refused = await SendAsync(HttpMethod.Post, "/api/action-items", w.Outsider, ItemBody(w.WorkspaceId, null, w.Outsider.UserId));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(refused)).Should().Be(ProjectAccessDeniedException.CreateItemMembersOnly);
        (await StatusAsync(HttpMethod.Post, "/api/action-items", w.Lead, ItemBody(w.WorkspaceId, null, w.Lead.UserId))).Should().Be(HttpStatusCode.Forbidden,
            "the Project Managers list lets a person create projects, not standalone items");
        (await StatusAsync(HttpMethod.Post, "/api/action-items", p.Member, ItemBody(w.WorkspaceId, null, p.Member.UserId))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(HttpMethod.Post, "/api/action-items", p.PmoHead, ItemBody(w.WorkspaceId, null, p.PmoHead.UserId))).Should().Be(HttpStatusCode.Created);

        object ProjectBody(Session s) => new
        {
            name = "Created by " + s.UserId, workspaceId = w.WorkspaceId, projectType = "operational", priority = "medium",
            projectManagerUserId = w.Lead.UserId, sponsorUserIds = new[] { s.UserId }, plannedStartDate = Start, plannedEndDate = End,
        };
        var noProject = await SendAsync(HttpMethod.Post, "/api/projects", w.Outsider, ProjectBody(w.Outsider));
        noProject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(noProject)).Should().Be(ProjectAccessDeniedException.CreateProjectMembersOnly);
        (await StatusAsync(HttpMethod.Post, "/api/projects", w.Lead, ProjectBody(w.Lead))).Should().Be(HttpStatusCode.Created, "on the Project Managers list");
        (await StatusAsync(HttpMethod.Post, "/api/projects", p.Member, ProjectBody(p.Member))).Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Moving_an_item_needs_the_manage_right_on_the_project_it_leaves_and_the_right_to_add_where_it_lands()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        var other = await AddWorkspaceAsync("P-2b other", [w.WsAdmin.UserId], []);

        // The team member works on their item but may not take it out of the project.
        var leave = await SendAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", w.Team, new { projectId = Guid.Empty });
        leave.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(leave)).Should().Be(ProjectAccessDeniedException.ManagersOnly);

        // The standalone item's creator may not move it into the project (they do not manage it) ...
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", p.Creator, new { projectId = w.ProjectId })).Should().Be(HttpStatusCode.Forbidden);
        // ... nor into a workspace they are not part of.
        var away = await SendAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", p.Creator, new { workspaceId = other });
        away.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(away)).Should().Be(ProjectAccessDeniedException.CreateItemMembersOnly);

        // The lead PM takes an item out of their project (it stays in the workspace).
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", w.Lead, new { projectId = Guid.Empty })).Should().Be(HttpStatusCode.OK);
        // The workspace admin moves the standalone item to a workspace they administer.
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{p.StandaloneId}", w.WsAdmin, new { workspaceId = other })).Should().Be(HttpStatusCode.OK);
    }

    // ── Comments and files (B6) ─────────────────────────────────────────────

    [Fact]
    public async Task Authors_edit_their_own_comments_and_uploaders_delete_their_own_project_files_after_losing_the_manage_right()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        var pm = w.AdditionalPm;

        var comment = await ReadAsync<ActionTracker.Application.Features.Comments.DTOs.CommentResponseDto>(
            await SendAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={w.ProjectId}", pm, new { content = "Mine" }));
        var file = await ReadAsync<DocumentResponseDto>(await UploadAsync(pm, "Project", w.ProjectId));
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/people/projectManager/{pm.UserId}", w.WsAdmin)).Should().Be(HttpStatusCode.OK);

        // No longer a manager: no new comment or file ...
        (await StatusAsync(HttpMethod.Post, $"/api/comments?entityType=Project&entityId={w.ProjectId}", pm, new { content = "New" })).Should().Be(HttpStatusCode.Forbidden);
        (await UploadAsync(pm, "Project", w.ProjectId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // ... but their own comment and file stay theirs; someone else's do not.
        (await StatusAsync(HttpMethod.Put, $"/api/comments/{comment.Id}", pm, new { content = "Mine, edited" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/comments/{w.CommentId}", pm, new { content = "Not mine" })).Should().Be(HttpStatusCode.Forbidden);
        var files = await ReadAsync<List<DocumentResponseDto>>(await SendAsync(HttpMethod.Get, $"/api/documents?entityType=Project&entityId={w.ProjectId}", pm));
        files.Single(d => d.Id == file.Id).CanDelete.Should().BeTrue();
        files.Single(d => d.Id == w.ProjectDocId).CanDelete.Should().BeFalse();
        (await StatusAsync(HttpMethod.Delete, $"/api/documents/{w.ProjectDocId}", pm)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Delete, $"/api/documents/{file.Id}", pm)).Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(HttpMethod.Delete, $"/api/comments/{comment.Id}", pm)).Should().Be(HttpStatusCode.NoContent);
    }

    // ── Give direction (B4) ─────────────────────────────────────────────────

    [Fact]
    public async Task Direction_is_given_on_an_escalated_item_by_its_creator_or_its_managers_never_by_an_assignee()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var id in new[] { w.ItemTeamId, p.StandaloneId })
                (await db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == id)).IsEscalated = true;
            await db.SaveChangesAsync();
        }
        Task<HttpStatusCode> Direct(Session s, Guid item) =>
            StatusAsync(HttpMethod.Post, "/api/action-items/workflow/give-direction", s, new { actionItemId = item, directionText = "Do it" });
        async Task<bool?> Flag(Session s, Guid item) =>
            (await ReadAsync<ActionItemResponseDto>(await SendAsync(HttpMethod.Get, $"/api/action-items/{item}", s))).CanGiveDirection;

        // Project item: the manager yes, the assignee and a member no.
        (await Flag(w.Team, w.ItemTeamId)).Should().BeFalse();
        (await Direct(w.Team, w.ItemTeamId)).Should().Be(HttpStatusCode.Forbidden);
        (await Direct(p.Member, w.ItemTeamId)).Should().Be(HttpStatusCode.Forbidden);
        (await Flag(w.Lead, w.ItemTeamId)).Should().BeTrue();
        (await Direct(w.Lead, w.ItemTeamId)).Should().Be(HttpStatusCode.OK);

        // Standalone: the creator, a workspace admin and the Admin role yes; the assignee and the PMO Head no.
        (await Flag(p.Assignee, p.StandaloneId)).Should().BeFalse();
        var byAssignee = await SendAsync(HttpMethod.Post, "/api/action-items/workflow/give-direction", p.Assignee, new { actionItemId = p.StandaloneId, directionText = "x" });
        byAssignee.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await MessageAsync(byAssignee)).Should().Be(ProjectAccessDeniedException.StandaloneDirectionOnly);
        (await Direct(p.PmoHead, p.StandaloneId)).Should().Be(HttpStatusCode.Forbidden);
        (await Flag(p.Creator, p.StandaloneId)).Should().BeTrue();
        (await Direct(p.Creator, p.StandaloneId)).Should().Be(HttpStatusCode.OK);
        (await Direct(w.WsAdmin, p.StandaloneId)).Should().Be(HttpStatusCode.OK);
        (await Direct(p.AdminRole, p.StandaloneId)).Should().Be(HttpStatusCode.OK);

        // Not escalated: 409, and no flag.
        (await Flag(w.Lead, w.ItemOtherId)).Should().BeFalse();
        (await Direct(w.Lead, w.ItemOtherId)).Should().Be(HttpStatusCode.Conflict);
    }

    // ── Project status (fix 1, B5) ──────────────────────────────────────────

    private object StatusPut(World w, string status) => new
    {
        name = "P-2b status", projectType = "operational", status, priority = "medium",
        projectManagerUserId = w.Lead.UserId, sponsorUserIds = new[] { w.Sponsor.UserId },
        plannedStartDate = Start, plannedEndDate = End,
    };

    private async Task SetStatusAsync(Guid projectId, ProjectStatus status)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Projects.IgnoreQueryFilters().FirstAsync(x => x.Id == projectId)).Status = status;
        await db.SaveChangesAsync();
    }

    private async Task<ProjectStatus> StatusOfAsync(Guid projectId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Projects.IgnoreQueryFilters().AsNoTracking().FirstAsync(x => x.Id == projectId)).Status;
    }

    [Theory]
    [InlineData("active")]
    [InlineData("onHold")]
    [InlineData("completed")]
    [InlineData("pendingApproval")]
    public async Task A_plain_edit_cannot_take_a_Draft_project_out_of_Draft_except_to_cancel_it(string target)
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        foreach (var who in new[] { w.Lead, w.WsAdmin, p.AdminRole })
        {
            var response = await SendAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", who, StatusPut(w, target));
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await MessageAsync(response)).Should().Be(target == "pendingApproval"
                ? ProjectStatusRules.SubmitToRequestApproval : ProjectStatusRules.SubmitToActivate);
        }
        (await StatusOfAsync(w.ProjectId)).Should().Be(ProjectStatus.Draft);

        var dto = await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", w.Lead));
        dto.AllowedStatuses.Should().BeEquivalentTo(new[] { ProjectStatus.Draft, ProjectStatus.Cancelled });

        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "draft"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "cancelled"))).Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(w.ProjectId)).Should().Be(ProjectStatus.Cancelled);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "draft"))).Should().Be(HttpStatusCode.BadRequest, "a closed project stays closed");
    }

    [Fact]
    public async Task An_approved_project_moves_between_Active_and_On_Hold_and_never_back_to_Draft()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        await MakeSubmittableAsync(w, ProjectType.Operational);   // entering Active still checks milestones and items, as before
        await SetStatusAsync(w.ProjectId, ProjectStatus.Active);

        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "onHold"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "active"))).Should().Be(HttpStatusCode.OK);
        var back = await SendAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "draft"));
        back.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(back)).Should().Be(ProjectStatusRules.NoReturnToDraft);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "pendingApproval"))).Should().Be(HttpStatusCode.BadRequest);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "cancelled"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.Lead, StatusPut(w, "active"))).Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A milestone in every phase, each with an action item, so Submit's checks pass.</summary>
    private async Task MakeSubmittableAsync(World w, ProjectType type, string? name = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var project = await db.Projects.IgnoreQueryFilters().FirstAsync(x => x.Id == w.ProjectId);
        project.ProjectType = type;
        if (name != null) project.Name = name;
        foreach (var phase in Enum.GetValues<ProjectPhase>().Where(ph => ph != ProjectPhase.Initiation))
        {
            var m = NewMilestone(w.ProjectId, phase.ToString(), phase);
            db.Milestones.Add(m);
            db.ActionItems.Add(NewItem(w.WorkspaceId, w.ProjectId, m.Id, "Phase " + phase, w.Team.UserId));
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Submit_still_activates_an_operational_project_and_the_Admin_role_may_press_it()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        await MakeSubmittableAsync(w, ProjectType.Operational);

        (await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", p.AdminRole))).CanSubmit.Should().BeTrue();
        (await ReadAsync<ProjectResponseDto>(await SendAsync(HttpMethod.Get, $"/api/projects/{w.ProjectId}", w.WsAdmin))).CanSubmit.Should().BeFalse(
            "B5: a workspace admin manages the project but does not submit it");
        var byAdmin = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", w.WsAdmin, new { projectId = w.ProjectId, reason = "Ready" });
        byAdmin.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await MessageAsync(byAdmin)).Should().Be(ProjectWorkflowService.SubmitNotAllowedMessage);
        var validation = await SendAsync(HttpMethod.Get, $"/api/projects/workflow/validate-submit/{w.ProjectId}", p.AdminRole);
        (await validation.Content.ReadAsStringAsync()).Should().Contain("\"isValid\":true");

        var submit = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", p.AdminRole, new { projectId = w.ProjectId, reason = "Ready" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        (await StatusOfAsync(w.ProjectId)).Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public async Task An_Admin_submitted_strategic_project_is_routed_exactly_as_a_PM_submitted_one()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        await MakeSubmittableAsync(w, ProjectType.Strategic, name: "<b>Strategic</b> & co");
        var approver = await SignInAsync("p2b.approver." + Guid.NewGuid().ToString("N")[..6], AppRoles.Admin);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WorkspaceStrategicProjectApprovers.Add(new WorkspaceStrategicProjectApprover
                { WorkspaceId = w.WorkspaceId, UserId = approver.UserId, UserName = approver.UserId, Order = 1 });
            await db.SaveChangesAsync();
        }

        var submit = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", p.AdminRole, new { projectId = w.ProjectId, reason = "Ready <now>" });
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.ProjectApprovalRequests.SingleAsync(r => r.ProjectId == w.ProjectId);
            request.AssignedReviewerUserId.Should().Be(approver.UserId, "the workspace's chain, unchanged");
            request.Status.Should().Be(ProjectApprovalStatus.Pending);
            request.RequestedByUserId.Should().Be(p.AdminRole.UserId);
            var lead = await db.Users.FirstAsync(u => u.Id == w.Lead.UserId);
            request.RequestedByDisplayName.Should().Be(lead.FullName, "the request still names the lead PM, as for an additional PM");
        }

        // Fix 7: the approval e-mail's values are HTML-encoded.
        var mails = await Factory.Emails.WaitForAsync(e => e.TemplateKey == "ProjectApproval.Requested" && e.RelatedEntityId == w.ProjectId);
        mails.Should().ContainSingle();
        mails[0].Placeholders["ProjectName"].Should().Be("&lt;b&gt;Strategic&lt;/b&gt; &amp; co");
        mails[0].Placeholders["Reason"].Should().Be("Ready &lt;now&gt;");
    }

    [Fact]
    public async Task A_refused_strategic_submit_says_why_and_the_validation_names_an_inactive_first_approver()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        await MakeSubmittableAsync(w, ProjectType.Strategic);

        // No chain: validate-submit lists it, the submit is 422 with the same kind of reason (not only "Unprocessable Entity").
        var validation = await SendAsync(HttpMethod.Get, $"/api/projects/workflow/validate-submit/{w.ProjectId}", w.Lead);
        (await validation.Content.ReadAsStringAsync()).Should().Contain("no Strategic Project Approvers configured");
        var noChain = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", w.Lead, new { projectId = w.ProjectId, reason = "Ready" });
        noChain.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await MessageAsync(noChain)).Should().StartWith("This workspace has no Strategic Project Approvers configured.");

        // An inactive first approver: the validation now names them; the submit refuses with its reason.
        string approverId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var off = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), UserName = "p2b.off." + Guid.NewGuid().ToString("N")[..6], Email = "off@p2b.test",
                FirstName = "Retired", LastName = "Approver", IsActive = false,
            };
            db.Users.Add(off);
            db.WorkspaceStrategicProjectApprovers.Add(new WorkspaceStrategicProjectApprover
                { WorkspaceId = w.WorkspaceId, UserId = off.Id, UserName = off.UserName!, Order = 1 });
            await db.SaveChangesAsync();
            approverId = off.Id;
        }
        var inactive = await ReadAsync<ActionTracker.Application.Features.Projects.DTOs.SubmitValidationResultDto>(
            await SendAsync(HttpMethod.Get, $"/api/projects/workflow/validate-submit/{w.ProjectId}", w.Lead));
        inactive.IsValid.Should().BeFalse();
        inactive.Errors.Should().ContainSingle().Which.Should().Be(ProjectWorkflowService.FirstApproverInactiveMessage("Retired Approver"));
        var refused = await SendAsync(HttpMethod.Post, "/api/projects/workflow/submit", w.Lead, new { projectId = w.ProjectId, reason = "Ready" });
        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await MessageAsync(refused)).Should().Be("The first approver in the chain is inactive.");
        (await StatusOfAsync(w.ProjectId)).Should().Be(ProjectStatus.Draft);

        // An active first approver: valid, and the routing is unchanged.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Users.FirstAsync(u => u.Id == approverId)).IsActive = true;
            await db.SaveChangesAsync();
        }
        (await ReadAsync<ActionTracker.Application.Features.Projects.DTOs.SubmitValidationResultDto>(
            await SendAsync(HttpMethod.Get, $"/api/projects/workflow/validate-submit/{w.ProjectId}", w.Lead))).IsValid.Should().BeTrue();
        (await StatusAsync(HttpMethod.Post, "/api/projects/workflow/submit", w.Lead, new { projectId = w.ProjectId, reason = "Ready" })).Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(w.ProjectId)).Should().Be(ProjectStatus.PendingApproval);
    }

    // ── Restore (fix 10) and pending-summary (fix 8) ────────────────────────

    [Fact]
    public async Task Restoring_a_project_brings_back_only_what_was_deleted_with_it()
    {
        var p = await BuildPublicAsync();
        var w = p.W;
        var extra = NewMilestone(w.ProjectId, "Deleted on its own", ProjectPhase.Planning);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Milestones.Add(extra);
            await db.SaveChangesAsync();
        }
        (await StatusAsync(HttpMethod.Delete, $"/api/action-items/{w.ItemOtherId}", w.Lead)).Should().Be(HttpStatusCode.NoContent);
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}/milestones/{extra.Id}", w.Lead)).Should().Be(HttpStatusCode.NoContent);
        await Task.Delay(20);   // a later save has a later stamp
        (await StatusAsync(HttpMethod.Delete, $"/api/projects/{w.ProjectId}", w.Lead)).Should().Be(HttpStatusCode.NoContent);

        (await StatusAsync(HttpMethod.Patch, $"/api/projects/{w.ProjectId}/restore", p.Member)).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Patch, $"/api/projects/{w.ProjectId}/restore", w.Lead)).Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        using var check = Factory.Services.CreateScope();
        var cdb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await cdb.ActionItems.IgnoreQueryFilters().Where(a => a.ProjectId == w.ProjectId).ToListAsync();
        items.Single(a => a.Id == w.ItemOtherId).IsDeleted.Should().BeTrue("it was deleted before the project");
        items.Single(a => a.Id == w.ItemTeamId).IsDeleted.Should().BeFalse();
        var milestones = await cdb.Milestones.IgnoreQueryFilters().Where(m => m.ProjectId == w.ProjectId).ToListAsync();
        milestones.Single(m => m.Id == extra.Id).IsDeleted.Should().BeTrue();
        milestones.Single(m => m.Id == w.MilestoneId).IsDeleted.Should().BeFalse();
        (await cdb.ActionItemSubItems.IgnoreQueryFilters().FirstAsync(s => s.Id == w.SubTeamId)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task The_pending_badge_counts_only_projects_the_caller_can_see()
    {
        var hidden = await BuildWorldAsync();                  // private
        var visible = await BuildPublicAsync();
        var reviewer = await SignInAsync("p2b.reviewer." + Guid.NewGuid().ToString("N")[..6], AppRoles.TeamMember);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var projectId in new[] { hidden.ProjectId, visible.W.ProjectId })
                db.ProjectApprovalRequests.Add(new ProjectApprovalRequest
                {
                    Id = Guid.NewGuid(), ProjectId = projectId, RequestedByUserId = "x", RequestedByDisplayName = "x",
                    AssignedReviewerUserId = reviewer.UserId, AssignedReviewerDisplayName = "r", Status = ProjectApprovalStatus.Pending,
                    Reason = "r", CreatedAt = DateTime.UtcNow, Level = 1, TotalLevels = 1,
                });
            await db.SaveChangesAsync();
        }

        var summary = await SendAsync(HttpMethod.Get, "/api/projects/workflow/pending-summary", reviewer);
        (await ReadAsync<ProjectApprovalSummaryDto>(summary)).PendingProjectApprovals.Should().Be(1);
        (await ReadAsync<List<ProjectApprovalRequestDto>>(await SendAsync(HttpMethod.Get, "/api/projects/workflow/pending-reviews", reviewer)))
            .Should().ContainSingle(r => r.ProjectId == visible.W.ProjectId);
    }

    // ── Workspace PM list / approvers (fix 5) ───────────────────────────────

    [Fact]
    public async Task The_Project_Managers_list_and_the_approver_chain_refuse_an_empty_or_unknown_user()
    {
        var admin = await SignInAsync("p2b.fix5." + Guid.NewGuid().ToString("N")[..6], AppRoles.Admin);
        var ws = await AddWorkspaceAsync("P-2b fix 5", [admin.UserId], []);
        string inactive;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var u = await db.Users.FirstAsync(x => x.Id == admin.UserId);
            var off = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "off" + u.Id, Email = "off" + u.Email, FirstName = "Off", LastName = "Line", IsActive = false };
            db.Users.Add(off);
            await db.SaveChangesAsync();
            inactive = off.Id;
        }

        foreach (var (route, validatorMessage) in new[]
        {
            ("project-managers", "Choose the user to add to the Project Managers list."),
            ("strategic-project-approvers", "Choose the user to add as a strategic-project approver."),
        })
        {
            // The validator answers an empty body before the service is reached.
            var empty = await SendAsync(HttpMethod.Post, $"/api/workspaces/{ws}/{route}", admin, new { });
            empty.StatusCode.Should().Be(HttpStatusCode.BadRequest, route);
            (await empty.Content.ReadAsStringAsync()).Should().Contain(validatorMessage, route);
            (await StatusAsync(HttpMethod.Post, $"/api/workspaces/{ws}/{route}", admin, new { userId = "" })).Should().Be(HttpStatusCode.BadRequest, route);
            var unknown = await SendAsync(HttpMethod.Post, $"/api/workspaces/{ws}/{route}", admin, new { userId = Guid.NewGuid().ToString() });
            unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest, route);
            (await MessageAsync(unknown)).Should().Be("The selected user does not exist or is inactive.");
            (await StatusAsync(HttpMethod.Post, $"/api/workspaces/{ws}/{route}", admin, new { userId = inactive })).Should().Be(HttpStatusCode.BadRequest, route);
            (await StatusAsync(HttpMethod.Post, $"/api/workspaces/{ws}/{route}", admin, new { userId = admin.UserId })).Should().Be(HttpStatusCode.Created, route);
        }

        using var check = Factory.Services.CreateScope();
        var cdb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await cdb.WorkspaceProjectManagers.Where(x => x.WorkspaceId == ws).Select(x => x.UserId).ToListAsync()).Should().Equal(admin.UserId);
        (await cdb.WorkspaceStrategicProjectApprovers.Where(x => x.WorkspaceId == ws).Select(x => x.UserId).ToListAsync()).Should().Equal(admin.UserId);
    }

    // ── Private workspaces do not loosen ────────────────────────────────────

    [Fact]
    public async Task In_a_private_workspace_the_P2_rights_are_unchanged()
    {
        var w = await BuildWorldAsync(AppRoles.ProjectManager);   // private
        // Project-only PMs manage, a team member works on own items, viewer / sponsor read — as in P-2.
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", w.AdditionalPm, ProjectPut(w, "By the additional PM"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", w.Team, new { title = "Mine" })).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", w.Team, new { title = "Not mine" })).Should().Be(HttpStatusCode.Forbidden);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", w.Viewer, new { title = "x" })).Should().Be(HttpStatusCode.Forbidden);
        // An Admin with no link still does not see it (404, not 403).
        var admin = await SignInAsync("p2b.privadmin." + Guid.NewGuid().ToString("N")[..6], AppRoles.Admin);
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemTeamId}", admin, new { title = "x" })).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Put, $"/api/projects/{w.ProjectId}", admin, ProjectPut(w, "x"))).Should().Be(HttpStatusCode.NotFound);
        (await StatusAsync(HttpMethod.Post, "/api/projects/workflow/submit", admin, new { projectId = w.ProjectId, reason = "x" })).Should().Be(HttpStatusCode.NotFound);
        // A project-only team member who also created an item stays within the team-member ceiling.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ActionItems.IgnoreQueryFilters().FirstAsync(a => a.Id == w.ItemOtherId)).CreatedByUserId = w.Viewer.UserId;
            await db.SaveChangesAsync();
        }
        (await StatusAsync(HttpMethod.Put, $"/api/action-items/{w.ItemOtherId}", w.Viewer, new { title = "Created by me" })).Should().Be(HttpStatusCode.Forbidden);
    }
}
