using ActionTracker.Application.Features.Projects.DTOs;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using FluentAssertions;

namespace ActionTracker.Tests.Unit.ProjectClassifications;

/// <summary>
/// WO-2026-11 P-1 through <c>ProjectService</c>: a project has at most one
/// internal classification, optional, always of its own workspace (400
/// otherwise); an inactive one cannot be newly chosen but stays where it is;
/// leaving the field out keeps it; it can be changed in every status the edit
/// accepts (decision 4) while the date freeze still holds; the list filter
/// works by id; names never cross workspaces or leave a private workspace.
/// </summary>
public class ProjectInternalClassificationTests : ClassificationTestBase
{
    private static readonly DateTime Start = new(2030, 1, 7, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End   = new(2030, 6, 28, 0, 0, 0, DateTimeKind.Utc);

    private Task<ProjectResponseDto> CreateAsync(Guid workspaceId, Guid? classificationId, string user = Admin) =>
        As(user, () => Projects().CreateAsync(new ProjectCreateDto
        {
            Name = "New project " + Guid.NewGuid().ToString("N")[..6], WorkspaceId = workspaceId,
            ProjectType = ProjectType.Operational, Priority = ActionPriority.Medium,
            ProjectManagerUserId = Admin, SponsorUserIds = [Admin],
            PlannedStartDate = Start, PlannedEndDate = End,
            InternalClassificationId = classificationId,
        }, user, default));

    /// <summary>An update that repeats the stored project, changed by <paramref name="change"/>.</summary>
    private async Task<ProjectResponseDto> UpdateAsync(Guid projectId, Action<ProjectUpdateDto> change, string user = Admin)
    {
        var p = await RawProjectAsync(projectId);
        var dto = new ProjectUpdateDto
        {
            Name = p.Name, Description = p.Description, ProjectType = p.ProjectType, Status = p.Status,
            Priority = p.Priority, ProjectManagerUserId = p.ProjectManagerUserId, SponsorUserIds = [Admin],
            PlannedStartDate = p.PlannedStartDate, PlannedEndDate = p.PlannedEndDate, ActualStartDate = p.ActualStartDate,
        };
        change(dto);
        return await As(user, () => Projects().UpdateAsync(projectId, dto, default));
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_project_can_be_created_without_a_classification()
    {
        var created = await CreateAsync(Alpha, null);

        created.InternalClassificationId.Should().BeNull();
        created.InternalClassificationName.Should().BeNull();
        (await RawProjectAsync(created.Id)).InternalClassificationId.Should().BeNull();
    }

    [Fact]
    public async Task A_project_takes_an_active_classification_of_its_own_workspace()
    {
        var c = await AddClassificationAsync(Alpha, "Capital works");

        var created = await CreateAsync(Alpha, c.Id);

        created.Should().Match<ProjectResponseDto>(p =>
            p.InternalClassificationId == c.Id && p.InternalClassificationName == "Capital works"
            && p.InternalClassificationIsActive == true);
        (await RawProjectAsync(created.Id)).InternalClassificationId.Should().Be(c.Id);
        var fetched = await As(Admin, () => Projects().GetByIdAsync(created.Id, default));
        fetched!.InternalClassificationName.Should().Be("Capital works");
    }

    [Fact]
    public async Task Another_workspaces_classification_is_refused_without_naming_it()
    {
        var beta = await AddClassificationAsync(Beta, "Beta secret");

        var act = () => CreateAsync(Alpha, beta.Id);

        (await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The chosen internal classification does not belong to this project's workspace."))
            .Which.Message.Should().NotContain("Beta secret");
    }

    [Fact]
    public async Task An_unknown_or_deleted_classification_is_refused()
    {
        var gone = await AddClassificationAsync(Alpha, "Gone", isDeleted: true);

        await ((Func<Task>)(() => CreateAsync(Alpha, Guid.NewGuid()))).Should().ThrowAsync<ArgumentException>();
        await ((Func<Task>)(() => CreateAsync(Alpha, gone.Id))).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task An_inactive_classification_cannot_be_chosen_on_create()
    {
        var off = await AddClassificationAsync(Alpha, "Retired", isActive: false);

        var act = () => CreateAsync(Alpha, off.Id);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The internal classification \"Retired\" is inactive and cannot be chosen.");
    }

    [Fact]
    public async Task A_private_workspaces_classification_cannot_be_used_by_a_non_member()
    {
        var secret = await AddClassificationAsync(Hidden, "Secret");

        // The outsider names a project of an open workspace with the private id.
        var act = () => CreateAsync(Alpha, secret.Id, user: Outsider);

        (await act.Should().ThrowAsync<ArgumentException>()).Which.Message.Should().NotContain("Secret");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Leaving_the_field_out_keeps_the_classification()
    {
        var c = await AddClassificationAsync(Alpha, "Capital works");
        var project = await AddProjectAsync(Alpha, c.Id);

        var updated = await UpdateAsync(project.Id, d => d.Name = "Renamed project");

        updated.InternalClassificationId.Should().Be(c.Id);
        (await RawProjectAsync(project.Id)).InternalClassificationId.Should().Be(c.Id);
    }

    [Fact]
    public async Task An_update_sets_changes_and_clears_the_classification()
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2);
        var project = await AddProjectAsync(Alpha);

        (await UpdateAsync(project.Id, d => d.InternalClassificationId = a.Id)).InternalClassificationName.Should().Be("A");
        (await UpdateAsync(project.Id, d => d.InternalClassificationId = b.Id)).InternalClassificationName.Should().Be("B");
        var cleared = await UpdateAsync(project.Id, d => d.ClearInternalClassification = true);

        cleared.InternalClassificationId.Should().BeNull();
        (await RawProjectAsync(project.Id)).InternalClassificationId.Should().BeNull();
    }

    [Fact]
    public async Task Setting_and_clearing_at_once_is_refused()
    {
        var a = await AddClassificationAsync(Alpha, "A");
        var project = await AddProjectAsync(Alpha);

        var act = () => UpdateAsync(project.Id, d => { d.InternalClassificationId = a.Id; d.ClearInternalClassification = true; });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not both*");
    }

    [Fact]
    public async Task A_deactivated_classification_stays_on_its_project_but_cannot_be_newly_chosen()
    {
        var retired = await AddClassificationAsync(Alpha, "Retired", isActive: false);
        var keeper  = await AddProjectAsync(Alpha, retired.Id);
        var other   = await AddProjectAsync(Alpha);

        // The project that has it keeps it — also when the save repeats the id.
        var kept = await UpdateAsync(keeper.Id, d => { d.Name = "Still retired"; d.InternalClassificationId = retired.Id; });
        kept.Should().Match<ProjectResponseDto>(p => p.InternalClassificationId == retired.Id && p.InternalClassificationIsActive == false);

        var act = () => UpdateAsync(other.Id, d => d.InternalClassificationId = retired.Id);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*is inactive and cannot be chosen*");
        (await RawProjectAsync(other.Id)).InternalClassificationId.Should().BeNull();
    }

    [Fact]
    public async Task An_update_cannot_move_the_classification_to_another_workspaces_one()
    {
        var a = await AddClassificationAsync(Alpha, "A");
        var beta = await AddClassificationAsync(Beta, "Beta");
        var project = await AddProjectAsync(Alpha, a.Id);

        var act = () => UpdateAsync(project.Id, d => d.InternalClassificationId = beta.Id);

        await act.Should().ThrowAsync<ArgumentException>();
        (await RawProjectAsync(project.Id)).InternalClassificationId.Should().Be(a.Id);
    }

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.OnHold)]
    [InlineData(ProjectStatus.PendingApproval)]
    [InlineData(ProjectStatus.Completed)]
    [InlineData(ProjectStatus.Cancelled)]
    public async Task The_classification_can_be_changed_after_approval_while_the_dates_stay_frozen(ProjectStatus status)
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2);
        var project = await AddProjectAsync(Alpha, a.Id, status: status);

        var updated = await UpdateAsync(project.Id, d => d.InternalClassificationId = b.Id);
        updated.InternalClassificationName.Should().Be("B");
        updated.Status.Should().Be(status);

        // The date freeze is unchanged: a date change is still refused.
        var act = () => UpdateAsync(project.Id, d => { d.InternalClassificationId = a.Id; d.PlannedEndDate = End.AddDays(3); });
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("Project dates cannot be changed*");
        (await RawProjectAsync(project.Id)).InternalClassificationId.Should().Be(b.Id, "a refused save changes nothing");
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_list_filters_by_classification_id_and_by_unclassified()
    {
        var a = await AddClassificationAsync(Alpha, "A", 1);
        var b = await AddClassificationAsync(Alpha, "B", 2);
        var withA = await AddProjectAsync(Alpha, a.Id);
        await AddProjectAsync(Alpha, b.Id);
        var none = await AddProjectAsync(Alpha);

        var byA = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, InternalClassificationId = a.Id, PageSize = 50 }, default));
        var unclassified = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, Unclassified = true, PageSize = 50 }, default));
        var all = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, PageSize = 50 }, default));

        byA.Items.Should().ContainSingle(p => p.Id == withA.Id && p.InternalClassificationName == "A");
        unclassified.Items.Should().ContainSingle(p => p.Id == none.Id && p.InternalClassificationId == null);
        all.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task The_include_deleted_list_shows_the_classification_too()
    {
        var a = await AddClassificationAsync(Alpha, "A");
        var deleted = await AddProjectAsync(Alpha, a.Id, isDeleted: true);

        var list = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, IncludeDeleted = true, PageSize = 50 }, default));

        list.Items.Should().ContainSingle(p => p.Id == deleted.Id && p.InternalClassificationName == "A");
    }

    [Fact]
    public async Task A_stray_link_to_another_workspaces_classification_is_never_shown()
    {
        // Not reachable through the API; written directly to prove the response guard.
        var beta = await AddClassificationAsync(Beta, "Beta secret");
        var project = await AddProjectAsync(Alpha, beta.Id);

        var list = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, PageSize = 50 }, default));
        var single = await As(Admin, () => Projects().GetByIdAsync(project.Id, default));
        var withDeleted = await As(Admin, () => Projects().GetAllAsync(new ProjectFilterDto { WorkspaceId = Alpha, IncludeDeleted = true, PageSize = 50 }, default));

        list.Items.Single().Should().Match<ProjectResponseDto>(p => p.InternalClassificationId == null && p.InternalClassificationName == null);
        single!.InternalClassificationName.Should().BeNull();
        withDeleted.Items.Single().InternalClassificationName.Should().BeNull();
    }

    [Fact]
    public async Task A_private_workspaces_classification_never_reaches_a_non_member()
    {
        var secret = await AddClassificationAsync(Hidden, "Secret");
        var hiddenProject = await AddProjectAsync(Hidden, secret.Id);

        var everything = await As(Outsider, () => Projects().GetAllAsync(new ProjectFilterDto { PageSize = 100 }, default));
        var byId = await As(Outsider, () => Projects().GetByIdAsync(hiddenProject.Id, default));
        var filtered = await As(Outsider, () => Projects().GetAllAsync(new ProjectFilterDto { InternalClassificationId = secret.Id, PageSize = 100 }, default));
        var withDeleted = await As(Outsider, () => Projects().GetAllAsync(new ProjectFilterDto { IncludeDeleted = true, PageSize = 100 }, default));

        everything.Items.Should().NotContain(p => p.InternalClassificationName == "Secret");
        byId.Should().BeNull();
        filtered.Items.Should().BeEmpty();
        withDeleted.Items.Should().NotContain(p => p.Id == hiddenProject.Id);

        // A member of the private workspace sees it.
        var asAdmin = await As(Admin, () => Projects().GetByIdAsync(hiddenProject.Id, default));
        asAdmin!.InternalClassificationName.Should().Be("Secret");
    }
}
