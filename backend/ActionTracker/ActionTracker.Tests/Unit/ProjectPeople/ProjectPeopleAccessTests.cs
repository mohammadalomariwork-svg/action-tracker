using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ProjectPeople;
using ActionTracker.Application.Features.ProjectPeople.DTOs;
using ActionTracker.Application.Features.ProjectPeople.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ActionTracker.Tests.Unit.ProjectPeople;

/// <summary>
/// WO-2026-11 P-2 rules on the EF model (InMemory): what each query filter lets a
/// project person see, the per-request role lookup and its reset, the
/// re-applied rule after IgnoreQueryFilters, the workspace-level rule, the access
/// decisions and the people rules.
/// </summary>
public class ProjectPeopleAccessTests : IDisposable
{
    private const string Admin = "u-admin", Lead = "u-lead", AddPm = "u-addpm", Sponsor = "u-sponsor",
        Viewer = "u-viewer", Team = "u-team", Outsider = "u-outsider", Inactive = "u-inactive";

    private readonly AppDbContext _db;
    private string? _user;
    private readonly Guid _ws = Guid.NewGuid(), _otherWs = Guid.NewGuid(), _project = Guid.NewGuid(), _sibling = Guid.NewGuid(),
        _deletedProject = Guid.NewGuid(), _milestone = Guid.NewGuid(), _sibMilestone = Guid.NewGuid(), _risk = Guid.NewGuid(),
        _teamItem = Guid.NewGuid(), _otherItem = Guid.NewGuid(), _sibItem = Guid.NewGuid(), _strayItem = Guid.NewGuid(),
        _sub = Guid.NewGuid(), _update = Guid.NewGuid(), _tag = Guid.NewGuid(), _class = Guid.NewGuid(), _sibClass = Guid.NewGuid();

    public ProjectPeopleAccessTests()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _user);
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, currentUser.Object);
        Seed();
    }

    public void Dispose() => _db.Dispose();

    private void Seed()
    {
        foreach (var id in new[] { Admin, Lead, AddPm, Sponsor, Viewer, Team, Outsider, Inactive })
            _db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@t", FirstName = id, LastName = "x", IsActive = id != Inactive });
        var ws = new Workspace { Id = _ws, Title = "Private", OrganizationUnit = "x", IsPrivate = true };
        ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = _ws, AdminUserId = Admin, AdminUserName = Admin });
        _db.Workspaces.AddRange(ws, new Workspace { Id = _otherWs, Title = "Other private", OrganizationUnit = "x", IsPrivate = true });
        _db.WorkspaceProjectManagers.AddRange(
            new WorkspaceProjectManager { WorkspaceId = _ws, UserId = Lead, UserName = Lead },
            new WorkspaceProjectManager { WorkspaceId = _ws, UserId = AddPm, UserName = AddPm });
        _db.WorkspaceProjectClassifications.AddRange(
            new WorkspaceProjectClassification { Id = _class, WorkspaceId = _ws, Name = "Own", NormalizedName = "OWN", SortOrder = 1, IsActive = true },
            new WorkspaceProjectClassification { Id = _sibClass, WorkspaceId = _ws, Name = "Sibling", NormalizedName = "SIBLING", SortOrder = 2, IsActive = true });

        var p = Project(_project, _ws, Lead);
        p.InternalClassificationId = _class;
        p.Sponsors.Add(new ProjectSponsor { ProjectId = _project, UserId = Sponsor });
        p.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = _project, UserId = AddPm, Role = ProjectMemberRole.ProjectManager });
        p.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = _project, UserId = Viewer, Role = ProjectMemberRole.Viewer });
        p.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = _project, UserId = Team, Role = ProjectMemberRole.TeamMember });
        var s = Project(_sibling, _ws, Admin);
        s.InternalClassificationId = _sibClass;
        var d = Project(_deletedProject, _ws, Viewer);
        d.IsDeleted = true;
        _db.Projects.AddRange(p, s, d);

        _db.Milestones.AddRange(
            new Milestone { Id = _milestone, MilestoneCode = "M1", Name = "M", ProjectId = _project },
            new Milestone { Id = _sibMilestone, MilestoneCode = "M2", Name = "MS", ProjectId = _sibling });
        _db.ProjectRisks.Add(new ProjectRisk { Id = _risk, RiskCode = "R1", ProjectId = _project, Title = "R" });

        _db.ActionItems.AddRange(
            Item(_teamItem, _ws, _project, Team), Item(_otherItem, _ws, _project, Admin), Item(_sibItem, _ws, _sibling, Admin),
            // An item that names the project but sits in another workspace is never reached through the role.
            Item(_strayItem, _otherWs, _project, Team));
        var sub = new ActionItemSubItem { Id = _sub, ActionItemId = _teamItem, Title = "Sub", EndDate = DateTime.UtcNow };
        sub.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = _sub, UserId = Team });
        _db.ActionItemSubItems.Add(sub);
        _db.ActionItemUpdates.Add(new ActionItemUpdate { Id = _update, ActionItemId = _teamItem, Content = "u", AuthorUserId = Admin, Status = ActionItemUpdateStatus.Submitted });
        _db.Tags.Add(new Tag { Id = _tag, WorkspaceId = _ws, Name = "t", NormalizedName = "T", CreatedByUserId = Admin });
        _db.ActionItemTags.Add(new ActionItemTag { ActionItemId = _teamItem, TagId = _tag });
        _user = null;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private static Project Project(Guid id, Guid ws, string lead) => new()
    {
        Id = id, ProjectCode = "P-" + id.ToString("N")[..4], Name = "P " + id, WorkspaceId = ws, ProjectManagerUserId = lead,
        ProjectType = ProjectType.Operational, Status = ProjectStatus.Draft,
    };

    private static ActionItem Item(Guid id, Guid ws, Guid? project, string assignee)
    {
        var item = new ActionItem { Id = id, ActionId = "A-" + id.ToString("N")[..4], Title = "I", WorkspaceId = ws, ProjectId = project, DueDate = DateTime.UtcNow };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = id, UserId = assignee });
        return item;
    }

    private void As(string? user)
    {
        _user = user;
        _db.ChangeTracker.Clear();
    }

    // ── Filters ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Lead)]
    [InlineData(AddPm)]
    [InlineData(Sponsor)]
    [InlineData(Viewer)]
    [InlineData(Team)]
    public void A_project_person_sees_the_project_and_everything_under_it_and_nothing_else(string user)
    {
        As(user);
        _db.Projects.Select(p => p.Id).Should().BeEquivalentTo(new[] { _project });
        _db.Projects.Include(p => p.Workspace).Single().Workspace.Should().NotBeNull("the workspace row joins (and nothing else of it)");
        _db.Milestones.Select(m => m.Id).Should().BeEquivalentTo(new[] { _milestone });
        _db.ProjectRisks.Select(r => r.Id).Should().BeEquivalentTo(new[] { _risk });
        _db.ActionItems.Select(a => a.Id).Should().BeEquivalentTo(new[] { _teamItem, _otherItem });
        _db.ActionItemSubItems.Select(x => x.Id).Should().BeEquivalentTo(new[] { _sub });
        _db.ActionItemSubItemAssignees.Should().ContainSingle();
        _db.ActionItemUpdates.Select(u => u.Id).Should().BeEquivalentTo(new[] { _update });
        _db.ActionItemTags.Should().ContainSingle();
        // The workspace's own data stays hidden.
        _db.Tags.Should().BeEmpty("tag suggestions are workspace-level");
        _db.WorkspaceProjectClassifications.Select(c => c.Id).Should().BeEquivalentTo(new[] { _class }, "only the project's own classification");
        _db.Workspaces.WhereWorkspaceLevelVisibleTo(user).Should().BeEmpty();
        _db.Workspaces.IgnoreQueryFilters().WhereWorkspaceLevelVisibleTo(user).Should().NotContain(w => w.Id == _ws);
    }

    [Fact]
    public void A_user_with_no_link_sees_nothing_of_the_private_workspace_and_a_member_sees_all()
    {
        As(Outsider);
        _db.Projects.Should().BeEmpty();
        _db.Workspaces.Should().BeEmpty();
        _db.ActionItems.Should().BeEmpty();
        _db.Milestones.Should().BeEmpty();
        _db.ActionItemUpdates.Should().BeEmpty();

        As(Admin);
        _db.Projects.Should().HaveCount(2);
        _db.ActionItems.Should().HaveCount(3);
        _db.Workspaces.WhereWorkspaceLevelVisibleTo(Admin).Select(w => w.Id).Should().Contain(_ws);
    }

    [Fact]
    public void Re_applied_after_IgnoreQueryFilters_the_rule_is_the_same()
    {
        As(Viewer);
        _db.Projects.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Select(p => p.Id)
            .Should().BeEquivalentTo(new[] { _project }, "a deleted project grants nothing");
        _db.ActionItems.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Select(a => a.Id).Should().BeEquivalentTo(new[] { _teamItem, _otherItem });
        _db.Milestones.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Select(m => m.Id).Should().BeEquivalentTo(new[] { _milestone });
        _db.ProjectRisks.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Select(r => r.Id).Should().BeEquivalentTo(new[] { _risk });
        _db.ActionItemSubItems.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Should().ContainSingle();
        _db.ActionItemTags.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Should().ContainSingle();

        As(Outsider);
        _db.Projects.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Should().BeEmpty();
        _db.ProjectRisks.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Should().BeEmpty();
    }

    [Fact]
    public void An_item_naming_the_project_from_another_workspace_stays_hidden_when_the_person_also_has_a_role_there()
    {
        // The viewer also views a project of the other private workspace, so that workspace's
        // row passes its filter. Only the item's own-workspace check then keeps the stray item
        // (which names _project but sits in _otherWs) out — and its sub-items, updates and tags with it.
        var otherProject = Guid.NewGuid();
        As(null);
        var op = Project(otherProject, _otherWs, Admin);
        op.People.Add(new ProjectMember { Id = Guid.NewGuid(), ProjectId = otherProject, UserId = Viewer, Role = ProjectMemberRole.Viewer });
        _db.Projects.Add(op);
        _db.SaveChanges();

        As(Viewer);
        _db.ProjectRoleWorkspaceIds.Should().Contain(_otherWs);
        _db.Projects.Select(p => p.Id).Should().BeEquivalentTo(new[] { _project, otherProject });
        _db.ActionItems.Select(a => a.Id).Should().BeEquivalentTo(new[] { _teamItem, _otherItem });
        _db.ActionItems.IgnoreQueryFilters().WhereWorkspaceVisibleTo(_db).Where(a => !a.IsDeleted)
            .Select(a => a.Id).Should().BeEquivalentTo(new[] { _teamItem, _otherItem });
    }

    [Fact]
    public async Task Removing_a_person_takes_effect_on_the_next_query_of_the_same_request()
    {
        As(Viewer);
        _db.ProjectRoleProjectIds.Should().Contain(_project);
        (await _db.Projects.AnyAsync()).Should().BeTrue();

        _db.ProjectMembers.Remove(await _db.ProjectMembers.SingleAsync(m => m.UserId == Viewer));
        await _db.SaveChangesAsync();

        _db.ProjectRoleProjectIds.Should().NotContain(_project);
        (await _db.Projects.AnyAsync()).Should().BeFalse();
        (await _db.ActionItems.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public void The_role_lookup_follows_the_current_user_and_is_empty_in_system_context()
    {
        As(null);
        _db.ProjectRoleProjectIds.Should().BeEmpty();
        _db.Projects.IgnoreQueryFilters().Count().Should().Be(3);
        As(Team);
        _db.ProjectRoleProjectIds.Should().BeEquivalentTo(new[] { _project });
        _db.ProjectRoleWorkspaceIds.Should().BeEquivalentTo(new[] { _ws });
        As(Viewer);
        _db.ProjectRoleProjectIds.Should().BeEquivalentTo(new[] { _project }, "the deleted project where they are lead grants nothing");
    }

    // ── Access decisions ────────────────────────────────────────────────────

    [Theory]
    [InlineData(Lead, true, true, true)]
    [InlineData(AddPm, true, true, true)]
    [InlineData(Team, false, true, false)]
    [InlineData(Viewer, false, false, false)]
    [InlineData(Sponsor, false, false, false)]
    [InlineData(Admin, true, true, true)]
    public async Task Who_may_manage_and_work(string user, bool manage, bool workOnOwnItem, bool workOnOthersItem)
    {
        As(user);
        var access = new ProjectAccessService(_db);
        (await access.GetAsync(_project))!.CanManage.Should().Be(manage);
        (await access.CanWorkOnItemAsync(_teamItem)).Should().Be(workOnOwnItem);
        (await access.CanWorkOnItemAsync(_otherItem)).Should().Be(workOnOthersItem);
        (await access.IsWorkspaceLevelAsync(_ws)).Should().Be(user == Admin);
    }

    [Fact]
    public async Task A_team_member_works_on_a_sub_item_assigned_to_them_and_hidden_things_are_not_found()
    {
        As(Team);
        var access = new ProjectAccessService(_db);
        (await access.CanWorkOnItemAsync(_teamItem, _sub)).Should().BeTrue();
        await FluentActions.Invoking(() => access.EnsureCanWorkOnItemAsync(_otherItem)).Should()
            .ThrowAsync<ProjectAccessDeniedException>().WithMessage("*assigned to you*");
        await FluentActions.Invoking(() => access.EnsureCanManageAsync(_project)).Should().ThrowAsync<ProjectAccessDeniedException>();
        await FluentActions.Invoking(() => access.EnsureCanManageAsync(_sibling)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => access.EnsureCanWorkOnItemAsync(_sibItem)).Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Invoking(() => access.EnsureCanWorkOnItemAsync(_strayItem)).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task In_system_context_everything_is_allowed()
    {
        As(null);
        var access = new ProjectAccessService(_db);
        (await access.GetAsync(_project))!.CanManage.Should().BeTrue();
        (await access.CanWorkOnItemAsync(_otherItem)).Should().BeTrue();
    }

    // ── People rules ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("viewer", "viewer")]
    [InlineData("TeamMember", "teamMember")]
    [InlineData(" projectManager ", "projectManager")]
    [InlineData("SPONSOR", "sponsor")]
    [InlineData("leadProjectManager", null)]
    [InlineData("owner", null)]
    [InlineData(null, null)]
    public void Roles_are_normalised(string? raw, string? expected) => ProjectPeopleRules.NormalizeRole(raw).Should().Be(expected);

    [Fact]
    public void Ids_are_cleaned() =>
        ProjectPeopleRules.CleanIds(new[] { " a ", "A", "", "b" }).Should().Equal("a", "b");

    [Fact]
    public async Task Staging_an_update_applies_the_rules_and_keeps_missing_lists()
    {
        As(Admin);
        var people = ProjectPeopleTestHelpers.NewPeopleService(_db);
        var project = await _db.Projects.SingleAsync(p => p.Id == _project);

        // null lists keep the members; sponsors are the full list.
        var added = await people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor }, new ProjectPeopleLists(null, null, null), true);
        added.Should().BeEmpty();
        await _db.SaveChangesAsync();
        (await _db.ProjectMembers.CountAsync(m => m.ProjectId == _project)).Should().Be(3);

        // An additional PM must be on the PM list, active, and not the lead.
        await FluentActions.Invoking(() => people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor },
            new ProjectPeopleLists(new[] { AddPm, Viewer }, null, null), true)).Should().ThrowAsync<ArgumentException>().WithMessage("*Project Managers list*");
        await FluentActions.Invoking(() => people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor },
            new ProjectPeopleLists(new[] { Lead }, null, null), true)).Should().ThrowAsync<ArgumentException>().WithMessage("*lead project manager*");
        await FluentActions.Invoking(() => people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor },
            new ProjectPeopleLists(null, new[] { Inactive }, null), true)).Should().ThrowAsync<ArgumentException>().WithMessage("*active*");

        // Without workspace-level access: no new project managers, no lead change (decision D4).
        _db.ChangeTracker.Clear();
        project = await _db.Projects.SingleAsync(p => p.Id == _project);
        await FluentActions.Invoking(() => people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor },
            new ProjectPeopleLists(new[] { AddPm, Lead + "x" }, null, null), false)).Should().ThrowAsync<ProjectAccessDeniedException>();
        project.ProjectManagerUserId = AddPm;
        await FluentActions.Invoking(() => people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor },
            new ProjectPeopleLists(null, null, null), false)).Should().ThrowAsync<ProjectAccessDeniedException>();

        // A new lead PM loses their additional-PM row and is told (decision D10); sponsors any active user.
        _db.ChangeTracker.Clear();
        project = await _db.Projects.SingleAsync(p => p.Id == _project);
        project.ProjectManagerUserId = AddPm;
        added = await people.StageForProjectUpdateAsync(project, Lead, new[] { Sponsor, Outsider }, new ProjectPeopleLists(null, null, null), true);
        await _db.SaveChangesAsync();
        added.Should().BeEquivalentTo(new[] { new ProjectPersonAdded(AddPm, "leadProjectManager"), new ProjectPersonAdded(Outsider, "sponsor") });
        (await _db.ProjectMembers.AnyAsync(m => m.UserId == AddPm && m.Role == ProjectMemberRole.ProjectManager)).Should().BeFalse();
    }
}
