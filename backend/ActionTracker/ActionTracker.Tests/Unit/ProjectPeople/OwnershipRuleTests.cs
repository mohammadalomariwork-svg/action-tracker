using ActionTracker.Application.Common;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Calendars.Interfaces;
using ActionTracker.Application.Features.Milestones.Services;
using ActionTracker.Application.Features.Notifications;
using ActionTracker.Application.Features.ProjectPeople;
using ActionTracker.Application.Features.ProjectPeople.Interfaces;
using ActionTracker.Application.Features.ProjectPeople.Services;
using ActionTracker.Application.Features.Projects;
using ActionTracker.Application.Features.Projects.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Tests.Unit.Tags;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ActionTracker.Tests.Unit.ProjectPeople;

/// <summary>
/// WO-2026-11 P-2b on the EF model (InMemory): for every kind of person and record,
/// each response flag equals the decision of the guard behind the matching write —
/// <c>canWork</c> / <c>canDelete</c> on items (the delete itself, run on a fresh copy),
/// <c>canManage</c> / <c>canSubmit</c> on projects, <c>canManageSettings</c> on
/// workspaces — in a public and a private workspace. So the UI, which follows the
/// flags, never offers what the server refuses. Also the status-transition table
/// (fix 1) and the e-mail encoding (fix 7).
/// </summary>
public class OwnershipRuleTests
{
    // People. "Pub" = public workspace, "Priv" = private one.
    private const string WsAdmin = "u-wsadmin", Member = "u-member", Lead = "u-lead", AddPm = "u-addpm",
        Sponsor = "u-sponsor", Viewer = "u-viewer", Team = "u-team", Creator = "u-creator", Assignee = "u-assignee",
        SubAssignee = "u-subassignee", Outsider = "u-outsider", AdminRole = "u-adminrole", PmoHead = "u-pmohead",
        PmoAnalyst = "u-pmoanalyst", PrivAdmin = "u-privadmin", PrivMember = "u-privmember", PrivLead = "u-privlead",
        PrivTeam = "u-privteam";

    private static readonly string[] People =
    {
        WsAdmin, Member, Lead, AddPm, Sponsor, Viewer, Team, Creator, Assignee, SubAssignee, Outsider,
        AdminRole, PmoHead, PmoAnalyst, PrivAdmin, PrivMember, PrivLead, PrivTeam,
    };

    private static readonly Guid Pub = Guid.NewGuid(), Priv = Guid.NewGuid(),
        DraftProject = Guid.NewGuid(), ActiveProject = Guid.NewGuid(), PrivProject = Guid.NewGuid(),
        ItemTeam = Guid.NewGuid(), ItemOther = Guid.NewGuid(), ItemActive = Guid.NewGuid(), ItemDeleted = Guid.NewGuid(),
        Standalone = Guid.NewGuid(), StandaloneDeleted = Guid.NewGuid(), PrivItem = Guid.NewGuid(), PrivStandalone = Guid.NewGuid(),
        SubOfStandalone = Guid.NewGuid();

    private static readonly Guid[] LiveItems = { ItemTeam, ItemOther, ItemActive, Standalone, PrivItem, PrivStandalone };
    private static readonly Guid[] DeletedItems = { ItemDeleted, StandaloneDeleted };

    private sealed class Ctx : IDisposable
    {
        public string? User;
        public readonly AppDbContext Db;

        public Ctx()
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(c => c.UserId).Returns(() => User);
            Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, currentUser.Object);
            Seed(Db);
        }

        /// <summary>A new context per user: roles and project-role ids are worked out once per context, as per request.</summary>
        public AppDbContext As(string? user)
        {
            User = user;
            Db.ChangeTracker.Clear();
            return Db;
        }

        public void Dispose() => Db.Dispose();
    }

    private static void Seed(AppDbContext db)
    {
        foreach (var id in People)
            db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@t", FirstName = id, LastName = "x", IsActive = true });
        TestRoles.Grant(db, AdminRole, AppRoles.Admin);
        TestRoles.Grant(db, PmoHead, AppRoles.PmoHead);
        TestRoles.Grant(db, PmoAnalyst, AppRoles.PmoAnalyst);
        // An Admin who is not part of the private workspace must not see it (P-2 stays).
        var pub = new Workspace { Id = Pub, Title = "Public", OrganizationUnit = "x" };
        pub.Admins.Add(new WorkspaceAdmin { WorkspaceId = Pub, AdminUserId = WsAdmin, AdminUserName = WsAdmin });
        foreach (var m in new[] { Member, Creator, Assignee, SubAssignee })
            pub.Members.Add(new WorkspaceMember { WorkspaceId = Pub, UserId = m, UserName = m });
        var priv = new Workspace { Id = Priv, Title = "Private", OrganizationUnit = "x", IsPrivate = true };
        priv.Admins.Add(new WorkspaceAdmin { WorkspaceId = Priv, AdminUserId = PrivAdmin, AdminUserName = PrivAdmin });
        priv.Members.Add(new WorkspaceMember { WorkspaceId = Priv, UserId = PrivMember, UserName = PrivMember });
        db.Workspaces.AddRange(pub, priv);

        var draft = Project(DraftProject, Pub, Lead, ProjectStatus.Draft);
        draft.Sponsors.Add(new ProjectSponsor { ProjectId = DraftProject, UserId = Sponsor });
        draft.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = DraftProject, UserId = AddPm, Role = ProjectMemberRole.ProjectManager });
        draft.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = DraftProject, UserId = Viewer, Role = ProjectMemberRole.Viewer });
        draft.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = DraftProject, UserId = Team, Role = ProjectMemberRole.TeamMember });
        var active = Project(ActiveProject, Pub, Lead, ProjectStatus.Active);
        var privProject = Project(PrivProject, Priv, PrivLead, ProjectStatus.Draft);
        privProject.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = PrivProject, UserId = PrivTeam, Role = ProjectMemberRole.TeamMember });
        db.Projects.AddRange(draft, active, privProject);

        db.ActionItems.AddRange(
            Item(ItemTeam, Pub, DraftProject, creator: null, Team),
            Item(ItemOther, Pub, DraftProject, creator: Creator, WsAdmin),
            Item(ItemActive, Pub, ActiveProject, creator: Creator, Assignee),
            Item(ItemDeleted, Pub, DraftProject, creator: Creator, Assignee, deleted: true),
            Item(Standalone, Pub, null, creator: Creator, Assignee),
            Item(StandaloneDeleted, Pub, null, creator: Creator, Assignee, deleted: true),
            Item(PrivItem, Priv, PrivProject, creator: PrivMember, PrivTeam),
            Item(PrivStandalone, Priv, null, creator: PrivMember, PrivMember));
        var sub = new ActionItemSubItem { Id = SubOfStandalone, ActionItemId = Standalone, Title = "Sub", EndDate = new DateTime(2030, 6, 28) };
        sub.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = SubOfStandalone, UserId = SubAssignee });
        db.ActionItemSubItems.Add(sub);
        db.SaveChanges();
    }

    private static Project Project(Guid id, Guid ws, string lead, ProjectStatus status) => new()
    {
        Id = id, ProjectCode = "PRJ-" + id.ToString("N")[..6], Name = "P " + id.ToString("N")[..4], WorkspaceId = ws,
        ProjectType = ProjectType.Operational, Status = status, Priority = ActionPriority.Medium, ProjectManagerUserId = lead,
        PlannedStartDate = new DateTime(2030, 6, 3), PlannedEndDate = new DateTime(2030, 6, 28), CreatedAt = DateTime.UtcNow,
    };

    private static ActionItem Item(Guid id, Guid ws, Guid? project, string? creator, string assignee, bool deleted = false)
    {
        var item = new ActionItem
        {
            Id = id, ActionId = "ACT-" + id.ToString("N")[..6], Title = "Item", Description = "", WorkspaceId = ws, ProjectId = project,
            IsStandalone = project == null, Status = ActionStatus.ToDo, Priority = ActionPriority.Medium, DueDate = new DateTime(2030, 6, 28),
            CreatedByUserId = creator, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, IsDeleted = deleted,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = id, UserId = assignee });
        return item;
    }

    private static ActionItemService ItemService(AppDbContext db) => new(
        db, NullLogger<ActionItemService>.Instance, Mock.Of<IEmailSender>(), Mock.Of<INotificationService>(),
        Options.Create(new AppSettings { FrontendBaseUrl = "http://localhost" }), Mock.Of<IServiceScopeFactory>(),
        Mock.Of<IWorkCalendarService>(), TagTestHelpers.NewTagService(db));

    private static async Task<bool> Succeeds(Func<Task> act)
    {
        try { await act(); return true; }
        catch (ProjectAccessDeniedException) { return false; }
        catch (KeyNotFoundException) { return false; }
        catch (ArgumentException) { return false; }          // a project item outside Draft
        catch (InvalidOperationException) { return false; }  // restore of an item whose project is deleted
    }

    private static async Task<List<ItemFacts>> VisibleFactsAsync(AppDbContext db, bool deleted)
    {
        var query = deleted
            ? db.ActionItems.IgnoreQueryFilters().Where(a => a.IsDeleted).WhereWorkspaceVisibleTo(db)
            : db.ActionItems.AsQueryable();
        return await query.AsNoTracking()
            .Select(a => new ItemFacts(a.Id, a.WorkspaceId, a.ProjectId, a.Assignees.Select(x => x.UserId).ToList(), a.IsDeleted))
            .ToListAsync();
    }

    [Fact]
    public async Task Item_flags_equal_the_work_and_delete_guards_for_everyone()
    {
        var mismatches = new List<string>();
        foreach (var user in People)
        {
            using var ctx = new Ctx();
            var db = ctx.As(user);
            var access = new ProjectAccessService(db);
            var facts = await VisibleFactsAsync(db, deleted: false);
            facts.AddRange(await VisibleFactsAsync(db, deleted: true));
            var rights = await access.GetItemRightsAsync(facts);

            foreach (var f in facts)
            {
                if (!f.IsDeleted)
                {
                    var canWork = await access.CanWorkOnItemAsync(f.ItemId);
                    if (rights[f.ItemId].CanWork != canWork) mismatches.Add($"{user} work {Name(f.ItemId)}: flag {rights[f.ItemId].CanWork}, guard {canWork}");
                }

                // The real delete / restore, on a copy of the data of its own.
                using var copy = new Ctx();
                var cdb = copy.As(user);
                var service = ItemService(cdb);
                var did = await Succeeds(() => f.IsDeleted ? service.RestoreAsync(f.ItemId, default) : service.DeleteAsync(f.ItemId, default));
                if (rights[f.ItemId].CanDelete != did) mismatches.Add($"{user} {(f.IsDeleted ? "restore" : "delete")} {Name(f.ItemId)}: flag {rights[f.ItemId].CanDelete}, server {did}");
            }
        }
        mismatches.Should().BeEmpty();
    }

    [Fact]
    public async Task The_matrix_matches_the_approved_rights_table()
    {
        using var ctx = new Ctx();
        async Task<bool> Works(string user, Guid item, Guid? sub = null) =>
            await new ProjectAccessService(ctx.As(user)).CanWorkOnItemAsync(item, sub);
        async Task<bool> Manages(string user, Guid project) =>
            (await new ProjectAccessService(ctx.As(user)).GetAsync(project))?.CanManage == true;

        // Manage a project: lead / additional PM, workspace admins, Admin, PMO Head.
        foreach (var u in new[] { Lead, AddPm, WsAdmin, AdminRole, PmoHead }) (await Manages(u, DraftProject)).Should().BeTrue(u);
        foreach (var u in new[] { Member, Sponsor, Viewer, Team, Creator, Assignee, Outsider, PmoAnalyst }) (await Manages(u, DraftProject)).Should().BeFalse(u);

        // Work on a project item: its managers, its creator and assignees.
        foreach (var u in new[] { Lead, AddPm, WsAdmin, AdminRole, PmoHead, Creator }) (await Works(u, ItemOther)).Should().BeTrue(u);
        foreach (var u in new[] { Member, Sponsor, Viewer, Team, Assignee, Outsider, PmoAnalyst }) (await Works(u, ItemOther)).Should().BeFalse(u);
        (await Works(Team, ItemTeam)).Should().BeTrue("the team member is its assignee");

        // A standalone item: creator, assignees, workspace admins, the Admin role; a sub-item's assignee only for that sub-item.
        foreach (var u in new[] { Creator, Assignee, WsAdmin, AdminRole }) (await Works(u, Standalone)).Should().BeTrue(u);
        foreach (var u in new[] { Member, Lead, AddPm, PmoHead, PmoAnalyst, SubAssignee, Outsider }) (await Works(u, Standalone)).Should().BeFalse(u);
        (await Works(SubAssignee, Standalone, SubOfStandalone)).Should().BeTrue();

        // Private workspace: P-2 unchanged; an unlinked Admin does not see it.
        (await Works(PrivTeam, PrivItem)).Should().BeTrue();
        (await Works(PrivMember, PrivItem)).Should().BeTrue("the creator, seen at workspace level");
        (await Works(PrivAdmin, PrivItem)).Should().BeTrue();
        (await Works(AdminRole, PrivItem)).Should().BeFalse();
        (await new ProjectAccessService(ctx.As(AdminRole)).GetAsync(PrivProject)).Should().BeNull();
        (await Manages(PrivTeam, PrivProject)).Should().BeFalse();
        (await Manages(PrivLead, PrivProject)).Should().BeTrue();
    }

    [Fact]
    public async Task Project_and_workspace_flags_equal_their_guards()
    {
        var mismatches = new List<string>();
        foreach (var user in People)
        {
            using var ctx = new Ctx();
            var db = ctx.As(user);
            var access = new ProjectAccessService(db);
            var projects = await db.Projects.AsNoTracking()
                .Select(p => new ProjectFacts(p.Id, p.WorkspaceId, p.ProjectManagerUserId)).ToListAsync();
            var many = await access.GetManyAsync(projects);
            foreach (var p in projects)
            {
                var single = await access.GetAsync(p.ProjectId);
                if (single == null) { mismatches.Add($"{user} sees {p.ProjectId} in the list but not by id"); continue; }
                if (many[p.ProjectId] != single) mismatches.Add($"{user} project {p.ProjectId}: batch {many[p.ProjectId]} vs single {single}");
                var guard = await Succeeds(() => access.EnsureCanManageAsync(p.ProjectId));
                if (many[p.ProjectId].CanManage != guard) mismatches.Add($"{user} manage {p.ProjectId}: flag {many[p.ProjectId].CanManage}, guard {guard}");
            }

            var workspaceIds = new[] { Pub, Priv };
            var manageable = await access.GetManageableWorkspaceIdsAsync(workspaceIds);
            foreach (var ws in workspaceIds)
            {
                var guard = await Succeeds(() => access.EnsureCanManageWorkspaceAsync(ws));
                if (manageable.Contains(ws) != guard) mismatches.Add($"{user} settings {ws}: flag {manageable.Contains(ws)}, guard {guard}");
            }
        }
        mismatches.Should().BeEmpty();

        using var check = new Ctx();
        (await new ProjectAccessService(check.As(WsAdmin)).GetManageableWorkspaceIdsAsync(new[] { Pub, Priv })).Should().BeEquivalentTo(new[] { Pub });
        (await new ProjectAccessService(check.As(AdminRole)).GetManageableWorkspaceIdsAsync(new[] { Pub, Priv })).Should().BeEquivalentTo(new[] { Pub });
        (await new ProjectAccessService(check.As(PrivAdmin)).GetManageableWorkspaceIdsAsync(new[] { Pub, Priv })).Should().BeEquivalentTo(new[] { Priv });
        (await new ProjectAccessService(check.As(Member)).GetManageableWorkspaceIdsAsync(new[] { Pub, Priv })).Should().BeEmpty();
        (await new ProjectAccessService(check.As(PmoHead)).GetManageableWorkspaceIdsAsync(new[] { Pub, Priv })).Should().BeEmpty("B1: PMO Head does not change workspace settings");
    }

    [Fact]
    public async Task Submit_is_for_the_projects_PMs_and_the_Admin_role()
    {
        using var ctx = new Ctx();
        async Task<bool> CanSubmit(string user) => (await new ProjectAccessService(ctx.As(user)).GetAsync(DraftProject))?.CanSubmit == true;
        foreach (var u in new[] { Lead, AddPm, AdminRole }) (await CanSubmit(u)).Should().BeTrue(u);
        foreach (var u in new[] { WsAdmin, PmoHead, Member, Sponsor, Viewer, Team, Outsider }) (await CanSubmit(u)).Should().BeFalse(u);
    }

    [Fact]
    public async Task Direction_follows_B4()
    {
        using var ctx = new Ctx();
        async Task<bool> Can(string user, Guid item) => await new ProjectAccessService(ctx.As(user)).CanGiveDirectionAsync(item);

        foreach (var u in new[] { Creator, Lead, AddPm, WsAdmin, AdminRole, PmoHead }) (await Can(u, ItemOther)).Should().BeTrue(u);
        foreach (var u in new[] { Member, Viewer, Sponsor, Team, Outsider }) (await Can(u, ItemOther)).Should().BeFalse(u);
        (await Can(Team, ItemTeam)).Should().BeFalse("an assignee never gives direction");
        foreach (var u in new[] { Creator, WsAdmin, AdminRole }) (await Can(u, Standalone)).Should().BeTrue(u);
        foreach (var u in new[] { Assignee, SubAssignee, Member, Lead, PmoHead }) (await Can(u, Standalone)).Should().BeFalse(u);
        (await Can(PrivTeam, PrivItem)).Should().BeFalse("the private team member is its assignee, not its creator");
        (await Can(PrivLead, PrivItem)).Should().BeTrue();
    }

    [Fact]
    public async Task Creating_follows_B2()
    {
        using var ctx = new Ctx();
        async Task<bool> Can(string user, Guid ws, bool project)
        {
            var guard = await Succeeds(() => new ProjectAccessService(ctx.As(user)).EnsureCanCreateInWorkspaceAsync(ws, project));
            var flag = await new ProjectAccessService(ctx.As(user)).CanCreateInWorkspaceAsync(ws, project);
            flag.Should().Be(guard, $"the create flag equals the guard for {user} (project: {project})");
            return guard;
        }

        foreach (var u in new[] { WsAdmin, Member, Creator, AdminRole, PmoHead, PmoAnalyst })
        {
            (await Can(u, Pub, project: false)).Should().BeTrue(u);
            (await Can(u, Pub, project: true)).Should().BeTrue(u);
        }
        foreach (var u in new[] { Lead, Sponsor, Viewer, Team, Outsider }) (await Can(u, Pub, project: false)).Should().BeFalse(u);
        (await Can(AdminRole, Priv, project: false)).Should().BeFalse("a private workspace stays hidden");
        (await Can(PrivMember, Priv, project: false)).Should().BeTrue();

        // The Project Managers list allows projects only.
        ctx.Db.WorkspaceProjectManagers.Add(new WorkspaceProjectManager { WorkspaceId = Pub, UserId = Lead, UserName = Lead });
        await ctx.Db.SaveChangesAsync();
        (await Can(Lead, Pub, project: true)).Should().BeTrue();
        (await Can(Lead, Pub, project: false)).Should().BeFalse();
    }

    [Theory]
    [InlineData(ProjectStatus.Draft, new[] { ProjectStatus.Draft, ProjectStatus.Cancelled })]
    [InlineData(ProjectStatus.PendingApproval, new[] { ProjectStatus.PendingApproval })]
    [InlineData(ProjectStatus.Active, new[] { ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled })]
    [InlineData(ProjectStatus.OnHold, new[] { ProjectStatus.OnHold, ProjectStatus.Active, ProjectStatus.Completed, ProjectStatus.Cancelled })]
    [InlineData(ProjectStatus.Completed, new[] { ProjectStatus.Completed })]
    [InlineData(ProjectStatus.Cancelled, new[] { ProjectStatus.Cancelled })]
    public void A_plain_edit_may_make_only_these_status_changes(ProjectStatus from, ProjectStatus[] allowed)
    {
        ProjectStatusRules.AllowedTargets(from).Should().BeEquivalentTo(allowed);
        foreach (var to in Enum.GetValues<ProjectStatus>().Except(allowed))
            ProjectStatusRules.RefusalReason(from, to).Should().NotBeNullOrWhiteSpace($"{from} → {to}");
    }

    [Fact]
    public void Project_and_milestone_email_values_are_HTML_encoded()
    {
        var project = Project(Guid.NewGuid(), Pub, Lead, ProjectStatus.Draft);
        project.Name = "<b>Big</b> & co";
        project.Description = "\"quoted\"";
        project.Workspace = new Workspace { Title = "R&D <ws>" };
        var p = ProjectService.BuildProjectPlaceholders(project, "http://x");
        p["ProjectName"].Should().Be("&lt;b&gt;Big&lt;/b&gt; &amp; co");
        p["Description"].Should().Be("&quot;quoted&quot;");
        p["WorkspaceName"].Should().Be("R&amp;D &lt;ws&gt;");

        var m = MilestoneService.BuildMilestonePlaceholders(new Milestone { Id = Guid.NewGuid(), MilestoneCode = "M<1>", Name = "Go & <live>" }, project, "http://x");
        m["MilestoneName"].Should().Be("Go &amp; &lt;live&gt;");
        m["ProjectName"].Should().Be("&lt;b&gt;Big&lt;/b&gt; &amp; co");
        m["MilestoneCode"].Should().Be("M&lt;1&gt;");
    }

    private static string Name(Guid id) =>
        id == ItemTeam ? "ItemTeam" : id == ItemOther ? "ItemOther" : id == ItemActive ? "ItemActive" : id == ItemDeleted ? "ItemDeleted"
        : id == Standalone ? "Standalone" : id == StandaloneDeleted ? "StandaloneDeleted" : id == PrivItem ? "PrivItem"
        : id == PrivStandalone ? "PrivStandalone" : id.ToString();
}
