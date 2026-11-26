using ActionTracker.Application.Features.ProjectClassifications;
using ActionTracker.Application.Features.ProjectClassifications.DTOs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Tests.Unit.ProjectClassifications;

/// <summary>
/// WO-2026-11 P-1: the workspace's list of internal classifications — add,
/// rename, reorder, deactivate, soft delete (blocked while any project, also a
/// soft-deleted one, uses it) — and who may read or change it (private
/// workspace filter, workspace access, the Project Managers list for reads).
/// </summary>
public class ProjectClassificationServiceTests : ClassificationTestBase
{
    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_cleans_the_name_and_appends_it_active()
    {
        var first  = await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "  Capital   works " }, Admin));
        var second = await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "Research" }, Admin));

        first.Should().Match<ProjectClassificationDto>(c =>
            c.Name == "Capital works" && c.SortOrder == 1 && c.IsActive && c.WorkspaceId == Alpha && c.CanDelete);
        second.SortOrder.Should().Be(2);
        var stored = await RawClassificationAsync(first.Id);
        stored.NormalizedName.Should().Be("CAPITAL WORKS");
        stored.CreatedByUserId.Should().Be(Admin);
    }

    [Theory]
    [InlineData("capital works")]
    [InlineData("CAPITAL  WORKS")]
    [InlineData(" Capital Works ")]
    public async Task A_name_the_workspace_already_has_in_any_case_is_a_conflict(string again)
    {
        await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "Capital Works" }, Admin));

        var act = () => As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = again }, Admin));

        await act.Should().ThrowAsync<ProjectClassificationConflictException>()
            .WithMessage("*already has a classification named*");
    }

    [Fact]
    public async Task The_same_name_is_fine_in_another_workspace_and_after_a_delete()
    {
        var alpha = await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "Capital works" }, Admin));
        await As(Admin, () => Classifications().CreateAsync(Beta, new() { Name = "Capital works" }, Admin));

        await As(Admin, () => Classifications().DeleteAsync(Alpha, alpha.Id, Admin));
        var again = await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "capital WORKS" }, Admin));

        again.Name.Should().Be("capital WORKS");
        again.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task A_broken_name_rule_is_an_ArgumentException()
    {
        var act = () => As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = new string('x', 101) }, Admin));
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*at most 100 characters*");
    }

    // ── Rename / (de)activate ─────────────────────────────────────────────────

    [Fact]
    public async Task Rename_changes_case_freely_but_not_to_another_classifications_name()
    {
        var capital = await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "Capital works" }, Admin));
        await As(Admin, () => Classifications().CreateAsync(Alpha, new() { Name = "Research" }, Admin));

        var recased = await As(Admin, () => Classifications().UpdateAsync(Alpha, capital.Id, new() { Name = "Capital Works", IsActive = true }, Admin));
        recased.Name.Should().Be("Capital Works");

        var act = () => As(Admin, () => Classifications().UpdateAsync(Alpha, capital.Id, new() { Name = "RESEARCH", IsActive = true }, Admin));
        await act.Should().ThrowAsync<ProjectClassificationConflictException>();
        (await RawClassificationAsync(capital.Id)).Name.Should().Be("Capital Works");
    }

    [Fact]
    public async Task Deactivate_and_reactivate_keep_the_projects_that_use_it()
    {
        var c = await AddClassificationAsync(Alpha, "Capital works");
        var project = await AddProjectAsync(Alpha, c.Id);

        var off = await As(Admin, () => Classifications().UpdateAsync(Alpha, c.Id, new() { Name = c.Name, IsActive = false }, Admin));
        off.IsActive.Should().BeFalse();
        off.ProjectCount.Should().Be(1);
        (await RawProjectAsync(project.Id)).InternalClassificationId.Should().Be(c.Id);

        var on = await As(Admin, () => Classifications().UpdateAsync(Alpha, c.Id, new() { Name = c.Name, IsActive = true }, Admin));
        on.IsActive.Should().BeTrue();
        (await RawClassificationAsync(c.Id)).UpdatedByUserId.Should().Be(Admin);
    }

    [Fact]
    public async Task A_classification_of_another_workspace_is_not_found_through_this_one()
    {
        var beta = await AddClassificationAsync(Beta, "Beta only");

        var act = () => As(Admin, () => Classifications().UpdateAsync(Alpha, beta.Id, new() { Name = "Stolen", IsActive = true }, Admin));

        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await RawClassificationAsync(beta.Id)).Name.Should().Be("Beta only");
    }

    // ── Reorder ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_sets_the_new_order_for_active_and_inactive_ones()
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2, isActive: false);
        var c = await AddClassificationAsync(Alpha, "C", 3);

        var list = await As(Admin, () => Classifications().ReorderAsync(Alpha, new() { ClassificationIds = [c.Id, a.Id, b.Id] }, Admin));

        list.Select(x => (x.Name, x.SortOrder)).Should().Equal(("C", 1), ("A", 2), ("B", 3));
    }

    [Fact]
    public async Task Reorder_must_list_every_live_classification_exactly_once()
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2);
        var other = await AddClassificationAsync(Beta, "Other", 1);
        var gone = await AddClassificationAsync(Alpha, "Gone", 3, isDeleted: true);

        foreach (var ids in new List<Guid>[] { [a.Id], [a.Id, b.Id, other.Id], [a.Id, other.Id], [a.Id, b.Id, gone.Id], [a.Id, a.Id] })
        {
            var act = () => As(Admin, () => Classifications().ReorderAsync(Alpha, new() { ClassificationIds = ids }, Admin));
            await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exactly once*");
        }

        (await RawClassificationAsync(a.Id)).SortOrder.Should().Be(1);
        (await RawClassificationAsync(other.Id)).SortOrder.Should().Be(1);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_soft_deletes_an_unused_one_and_keeps_the_order_dense()
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2);
        var c = await AddClassificationAsync(Alpha, "C", 3);

        await As(Admin, () => Classifications().DeleteAsync(Alpha, b.Id, Admin));

        var stored = await RawClassificationAsync(b.Id);
        stored.IsDeleted.Should().BeTrue("delete is a soft delete");
        var list = await As(Admin, () => Classifications().GetForWorkspaceAsync(Alpha, Admin));
        list.Select(x => (x.Name, x.SortOrder)).Should().Equal(("A", 1), ("C", 2));
        _ = a; _ = c;
    }

    [Fact]
    public async Task Delete_is_refused_while_a_live_project_uses_it()
    {
        var c = await AddClassificationAsync(Alpha, "Capital works");
        await AddProjectAsync(Alpha, c.Id);
        await AddProjectAsync(Alpha, c.Id);

        var act = () => As(Admin, () => Classifications().DeleteAsync(Alpha, c.Id, Admin));

        await act.Should().ThrowAsync<ProjectClassificationConflictException>()
            .WithMessage("\"Capital works\" is used by 2 projects and cannot be deleted.*deactivate it instead*");
        (await RawClassificationAsync(c.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_is_refused_while_only_a_soft_deleted_project_uses_it()
    {
        var c = await AddClassificationAsync(Alpha, "Capital works");
        await AddProjectAsync(Alpha, c.Id, isDeleted: true);

        var list = await As(Admin, () => Classifications().GetForWorkspaceAsync(Alpha, Admin));
        list.Single().Should().Match<ProjectClassificationDto>(x => x.ProjectCount == 0 && x.DeletedProjectCount == 1 && !x.CanDelete);

        var act = () => As(Admin, () => Classifications().DeleteAsync(Alpha, c.Id, Admin));
        await act.Should().ThrowAsync<ProjectClassificationConflictException>()
            .WithMessage("*used by 1 deleted project*");
    }

    [Fact]
    public async Task The_in_use_message_counts_live_and_deleted_projects()
    {
        var c = await AddClassificationAsync(Alpha, "Mixed");
        await AddProjectAsync(Alpha, c.Id);
        await AddProjectAsync(Alpha, c.Id, isDeleted: true);

        var act = () => As(Admin, () => Classifications().DeleteAsync(Alpha, c.Id, Admin));

        await act.Should().ThrowAsync<ProjectClassificationConflictException>()
            .WithMessage("*used by 2 projects (1 of them deleted)*");
    }

    // ── Who may read / change ─────────────────────────────────────────────────

    [Fact]
    public async Task A_private_workspaces_list_is_not_found_for_a_non_member_even_with_an_unrestricted_role()
    {
        await AddClassificationAsync(Hidden, "Secret name");
        VisibleWorkspaces = null;   // privileged role: the resolver lets everything through

        var read  = () => As(Outsider, () => Classifications().GetForWorkspaceAsync(Hidden, Outsider));
        var write = () => As(Outsider, () => Classifications().CreateAsync(Hidden, new() { Name = "X" }, Outsider));

        await read.Should().ThrowAsync<KeyNotFoundException>();
        await write.Should().ThrowAsync<KeyNotFoundException>();
        (await As(Admin, () => Classifications().GetForWorkspaceAsync(Hidden, Admin))).Should().ContainSingle(c => c.Name == "Secret name");
    }

    [Fact]
    public async Task The_query_filter_hides_a_private_workspaces_classifications_from_non_members()
    {
        await AddClassificationAsync(Hidden, "Secret name");
        await AddClassificationAsync(Alpha, "Open name");

        var asOutsider = await As(Outsider, () => Db.WorkspaceProjectClassifications.Select(c => c.Name).ToListAsync());
        var asAdmin    = await As(Admin,    () => Db.WorkspaceProjectClassifications.Select(c => c.Name).ToListAsync());

        asOutsider.Should().Equal("Open name");
        asAdmin.Should().BeEquivalentTo("Open name", "Secret name");
    }

    [Fact]
    public async Task A_workspace_the_caller_cannot_see_is_not_found()
    {
        VisibleWorkspaces = [Beta];

        var read  = () => As(Outsider, () => Classifications().GetForWorkspaceAsync(Alpha, Outsider));
        var write = () => As(Outsider, () => Classifications().CreateAsync(Alpha, new() { Name = "X" }, Outsider));

        await read.Should().ThrowAsync<KeyNotFoundException>();
        await write.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task A_project_manager_of_the_workspace_may_read_the_list_but_not_change_it()
    {
        await AddClassificationAsync(Alpha, "Capital works");
        VisibleWorkspaces = [];     // neither admin nor member anywhere

        var list = await As(Pm, () => Classifications().GetForWorkspaceAsync(Alpha, Pm));
        list.Should().ContainSingle(c => c.Name == "Capital works");

        var write = () => As(Pm, () => Classifications().CreateAsync(Alpha, new() { Name = "X" }, Pm));
        await write.Should().ThrowAsync<KeyNotFoundException>();

        var otherWorkspace = () => As(Pm, () => Classifications().GetForWorkspaceAsync(Beta, Pm));
        await otherWorkspace.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task The_list_is_in_sort_order_with_active_and_inactive_ones_and_without_deleted_ones()
    {
        await AddClassificationAsync(Alpha, "Second", 2, isActive: false);
        await AddClassificationAsync(Alpha, "First", 1);
        await AddClassificationAsync(Alpha, "Deleted", 3, isDeleted: true);
        await AddClassificationAsync(Beta, "Beta", 1);

        var list = await As(Admin, () => Classifications().GetForWorkspaceAsync(Alpha, Admin));

        list.Select(c => (c.Name, c.IsActive)).Should().Equal(("First", true), ("Second", false));
    }
}
