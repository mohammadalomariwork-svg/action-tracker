using System.Net;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// WO-2026-11 P-2. A private workspace W with project P — lead PM, an additional
/// PM, a sponsor, a viewer and a team member, none of them an admin or member of W
/// — and a sibling project S in W, each with a milestone, a risk, a comment, a
/// file and action items (with sub-items, an update and a file). Seeded in system
/// context; every user name is unique per world so worlds never share links.
/// </summary>
public abstract class ProjectPeopleIntegrationTestBase : TagsIntegrationTestBase
{
    protected static readonly DateTime Start = new(2030, 6, 3);   // Monday
    protected static readonly DateTime End   = new(2030, 6, 28);  // Friday

    protected ProjectPeopleIntegrationTestBase(ActionTrackerWebApplicationFactory factory) : base(factory) { }

    protected sealed class World
    {
        public Guid WorkspaceId;
        public Guid ProjectId, SiblingId;
        public Guid MilestoneId, SiblingMilestoneId, RiskId, SiblingRiskId;
        public Guid ItemTeamId, ItemOtherId, SiblingItemId;
        public Guid SubTeamId, SubOtherId, SiblingSubId;
        public Guid UpdateId, CommentId, SiblingCommentId;
        public Guid ItemDocId, ProjectDocId, SiblingDocId;
        public string Tag = "";
        public Session WsAdmin = null!, Lead = null!, AdditionalPm = null!, Sponsor = null!, Viewer = null!, Team = null!;
        public Session Outsider = null!;
        public string SiblingTitle = "";
    }

    /// <param name="linkRole">System role of the sponsor, viewer and team member (their permissions).</param>
    protected async Task<World> BuildWorldAsync(string linkRole = AppRoles.TeamMember, bool isPrivate = true)
    {
        var n = Guid.NewGuid().ToString("N")[..8];
        var w = new World
        {
            WsAdmin      = await SignInAsync($"p2.wsadmin.{n}", AppRoles.ProjectManager),
            Lead         = await SignInAsync($"p2.lead.{n}", AppRoles.ProjectManager),
            AdditionalPm = await SignInAsync($"p2.addpm.{n}", AppRoles.ProjectManager),
            Sponsor      = await SignInAsync($"p2.sponsor.{n}", linkRole),
            Viewer       = await SignInAsync($"p2.viewer.{n}", linkRole),
            Team         = await SignInAsync($"p2.team.{n}", linkRole),
            Outsider     = await SignInAsync($"p2.outsider.{n}", AppRoles.ProjectManager),
            SiblingTitle = $"Sibling secret {n}",
            Tag          = $"tag{n}",
        };
        w.WorkspaceId = await AddWorkspaceAsync("P-2", [w.WsAdmin.UserId], [], isPrivate);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var pm in new[] { w.WsAdmin.UserId, w.Lead.UserId, w.AdditionalPm.UserId })
            db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = w.WorkspaceId, UserId = pm, UserName = pm });

        var project = NewProject(w.WorkspaceId, "P-2 project " + n, w.Lead.UserId);
        project.Sponsors.Add(new ProjectSponsor { ProjectId = project.Id, UserId = w.Sponsor.UserId });
        project.People.Add(Member(project.Id, w.AdditionalPm.UserId, ProjectMemberRole.ProjectManager));
        project.People.Add(Member(project.Id, w.Viewer.UserId, ProjectMemberRole.Viewer));
        project.People.Add(Member(project.Id, w.Team.UserId, ProjectMemberRole.TeamMember));
        var sibling = NewProject(w.WorkspaceId, w.SiblingTitle, w.WsAdmin.UserId);
        sibling.Sponsors.Add(new ProjectSponsor { ProjectId = sibling.Id, UserId = w.WsAdmin.UserId });
        db.Projects.AddRange(project, sibling);
        (w.ProjectId, w.SiblingId) = (project.Id, sibling.Id);

        var m  = NewMilestone(project.Id, "M");
        var ms = NewMilestone(sibling.Id, "MS");
        db.Milestones.AddRange(m, ms);
        (w.MilestoneId, w.SiblingMilestoneId) = (m.Id, ms.Id);

        var r  = NewRisk(project.Id, "R");
        var rs = NewRisk(sibling.Id, "RS");
        db.ProjectRisks.AddRange(r, rs);
        (w.RiskId, w.SiblingRiskId) = (r.Id, rs.Id);

        var itemTeam  = NewItem(w.WorkspaceId, project.Id, m.Id, "Team item " + n, w.Team.UserId);
        var itemOther = NewItem(w.WorkspaceId, project.Id, m.Id, "Other item " + n, w.WsAdmin.UserId);
        var sibItem   = NewItem(w.WorkspaceId, sibling.Id, ms.Id, w.SiblingTitle + " item", w.WsAdmin.UserId);
        db.ActionItems.AddRange(itemTeam, itemOther, sibItem);
        (w.ItemTeamId, w.ItemOtherId, w.SiblingItemId) = (itemTeam.Id, itemOther.Id, sibItem.Id);

        var subTeam  = NewSub(itemTeam.Id, "Team sub", w.Team.UserId);
        var subOther = NewSub(itemOther.Id, "Other sub", w.WsAdmin.UserId);
        var subSib   = NewSub(sibItem.Id, "Sibling sub", w.WsAdmin.UserId);
        db.ActionItemSubItems.AddRange(subTeam, subOther, subSib);
        (w.SubTeamId, w.SubOtherId, w.SiblingSubId) = (subTeam.Id, subOther.Id, subSib.Id);

        var update = new ActionItemUpdate
        {
            Id = Guid.NewGuid(), ActionItemId = itemTeam.Id, Content = "Progress", AuthorUserId = w.WsAdmin.UserId,
            Status = ActionItemUpdateStatus.Submitted, SubmittedAt = DateTime.UtcNow,
        };
        db.ActionItemUpdates.Add(update);
        w.UpdateId = update.Id;

        var c  = new Comment { Id = Guid.NewGuid(), RelatedEntityType = "Project", RelatedEntityId = project.Id, Content = "Note", AuthorUserId = w.WsAdmin.UserId };
        var cs = new Comment { Id = Guid.NewGuid(), RelatedEntityType = "Project", RelatedEntityId = sibling.Id, Content = w.SiblingTitle, AuthorUserId = w.WsAdmin.UserId };
        db.Comments.AddRange(c, cs);
        (w.CommentId, w.SiblingCommentId) = (c.Id, cs.Id);

        var tag = new Tag { Id = Guid.NewGuid(), WorkspaceId = w.WorkspaceId, Name = w.Tag, NormalizedName = w.Tag.ToUpperInvariant(), CreatedByUserId = w.WsAdmin.UserId, CreatedAt = DateTime.UtcNow };
        db.Tags.Add(tag);
        db.ActionItemTags.Add(new ActionItemTag { ActionItemId = itemTeam.Id, TagId = tag.Id });

        w.ItemDocId    = AddDoc(db, "ActionItem", itemTeam.Id, w.WsAdmin.UserId);
        w.ProjectDocId = AddDoc(db, "Project", project.Id, w.WsAdmin.UserId);
        w.SiblingDocId = AddDoc(db, "Project", sibling.Id, w.WsAdmin.UserId);

        await db.SaveChangesAsync();
        return w;
    }

    protected static Project NewProject(Guid workspaceId, string name, string leadPm) => new()
    {
        Id = Guid.NewGuid(), ProjectCode = "PRJ-P2-" + Guid.NewGuid().ToString("N")[..6], Name = name,
        WorkspaceId = workspaceId, ProjectType = ProjectType.Operational, Status = ProjectStatus.Draft,
        Priority = ActionPriority.Medium, ProjectManagerUserId = leadPm, PlannedStartDate = Start, PlannedEndDate = End,
        CreatedAt = DateTime.UtcNow,
    };

    protected static ProjectMember Member(Guid projectId, string userId, ProjectMemberRole role) =>
        new() { Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, Role = role, AddedAt = DateTime.UtcNow };

    protected static Milestone NewMilestone(Guid projectId, string name, ProjectPhase phase = ProjectPhase.Initiation) => new()
    {
        Id = Guid.NewGuid(), MilestoneCode = "MS-P2-" + Guid.NewGuid().ToString("N")[..6], Name = name, ProjectId = projectId,
        SequenceOrder = 1, Phase = phase, PlannedStartDate = Start, PlannedDueDate = End,
    };

    protected static ProjectRisk NewRisk(Guid projectId, string title) => new()
    {
        Id = Guid.NewGuid(), RiskCode = "RISK-" + Guid.NewGuid().ToString("N")[..4], ProjectId = projectId, Title = title,
        Description = title, Category = "General", ProbabilityScore = 2, ImpactScore = 2, RiskScore = 4, RiskRating = RiskRating.Low,
    };

    protected static ActionItem NewItem(Guid workspaceId, Guid? projectId, Guid? milestoneId, string title, string assignee)
    {
        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-P2-" + Guid.NewGuid().ToString("N")[..6], Title = title, Description = "",
            WorkspaceId = workspaceId, ProjectId = projectId, MilestoneId = milestoneId, IsStandalone = projectId == null,
            Priority = ActionPriority.Medium, Status = ActionStatus.ToDo, StartDate = Start, DueDate = End,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = assignee });
        return item;
    }

    protected static ActionItemSubItem NewSub(Guid itemId, string title, string assignee)
    {
        var sub = new ActionItemSubItem { Id = Guid.NewGuid(), ActionItemId = itemId, Title = title, EndDate = End, SortOrder = 1 };
        sub.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = sub.Id, UserId = assignee });
        return sub;
    }

    protected static Guid AddDoc(AppDbContext db, string type, Guid entityId, string uploader)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(), Name = "Evidence", FileName = "evidence.pdf", ContentType = "application/pdf",
            FileSize = 4, Content = new byte[] { 37, 80, 68, 70 }, RelatedEntityType = type, RelatedEntityId = entityId,
            UploadedByUserId = uploader,
        };
        db.Documents.Add(doc);
        return doc.Id;
    }

    /// <summary>A user with an extra system role (permissions are read from the database on every request).</summary>
    protected async Task AddRoleAsync(Session user, string role)
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u = await users.FindByIdAsync(user.UserId);
        if (u != null && !await users.IsInRoleAsync(u, role)) await users.AddToRoleAsync(u, role);
    }

    protected async Task<HttpStatusCode> StatusAsync(HttpMethod method, string url, Session as_, object? body = null)
        => (await SendAsync(method, url, as_, body)).StatusCode;

    protected object ProjectPut(World w, string name, string? lead = null) => new
    {
        name, projectType = "operational", status = "draft", priority = "medium",
        projectManagerUserId = lead ?? w.Lead.UserId, sponsorUserIds = new[] { w.Sponsor.UserId },
        plannedStartDate = Start, plannedEndDate = End,
    };

    protected static object MilestoneBody(string name) => new
    {
        name, sequenceOrder = 2, phase = "planning", plannedStartDate = Start, plannedDueDate = End, status = "notStarted",
    };
}
