using System.Text;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Documents;
using ActionTracker.Application.Features.Documents.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Domain.Enums;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// WO-2026-11 A-4: the documents service checks the owning record on every
/// list / upload / download / delete. Hidden parent → 404 (also by document
/// id); action-item types need View / Edit; Done / Cancelled items, their
/// sub-items and submitted updates are locked (409); a draft update's files
/// belong to its author; delete rights on item / sub-item files (D4); caps;
/// project types as before but visibility-checked; KPI evidence: read scope
/// 404, write scope 403 as before (D3); a deleted draft's files stay but are
/// unreachable (D2).
/// </summary>
public class DocumentAccessTests : IDisposable
{
    private const string Creator  = "user-creator";   // workspace admin; creates the items
    private const string Assignee = "user-assignee";  // item assignee
    private const string SubOwner = "user-sub";       // sub-item assignee
    private const string Editor   = "user-editor";    // another workspace admin with ActionItems.Edit: works on the items (P-2b) but did not create them
    private const string AdminId  = "user-admin";     // Admin role
    private const string Outsider = "user-outsider";  // in no workspace list

    private readonly AppDbContext _db;
    private string? _currentUserId = Creator;
    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _privateWorkspaceId = Guid.NewGuid();
    private readonly Mock<IStrategicScopeService> _scope = new();
    private readonly HashSet<string> _admins = new() { AdminId };

    public DocumentAccessTests()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(() => _currentUserId);
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            currentUser.Object);

        foreach (var id in new[] { Creator, Assignee, SubOwner, Editor, AdminId, Outsider, "user-se", "user-se2" })
            _db.Users.Add(new ApplicationUser { Id = id, UserName = id, Email = id + "@t", FirstName = id, LastName = "X", CreatedAt = DateTime.UtcNow });

        var ws = new Workspace { Id = _workspaceId, Title = "Public", OrganizationUnit = "O", CreatedAt = DateTime.UtcNow };
        ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = _workspaceId, AdminUserId = Creator, AdminUserName = "c" });
        // WO-2026-11 P-2b: only linked people work on an item, so the "other editor" is a second admin.
        ws.Admins.Add(new WorkspaceAdmin { WorkspaceId = _workspaceId, AdminUserId = Editor, AdminUserName = "e" });
        foreach (var m in new[] { Assignee, SubOwner })
            ws.Members.Add(new WorkspaceMember { WorkspaceId = _workspaceId, UserId = m, UserName = m });
        var priv = new Workspace { Id = _privateWorkspaceId, Title = "Private", OrganizationUnit = "O", IsPrivate = true, CreatedAt = DateTime.UtcNow };
        priv.Admins.Add(new WorkspaceAdmin { WorkspaceId = _privateWorkspaceId, AdminUserId = Creator, AdminUserName = "c" });
        _db.Workspaces.AddRange(ws, priv);
        TestRoles.Grant(_db, AdminId, AppRoles.Admin);   // P-2b reads roles from the database
        _db.SaveChanges();

        _scope.Setup(s => s.GetVisibleOrgUnitIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Guid>?)null);
    }

    public void Dispose() => _db.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private DocumentService Service()
    {
        var roles = new Mock<IUserRoleService>();
        roles.Setup(r => r.IsInRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string user, string role, CancellationToken _) => role == AppRoles.Admin && _admins.Contains(user));
        return new DocumentService(_db, NullLogger<DocumentService>.Instance, _scope.Object, roles.Object);
    }

    private static DocumentCaller Can(string user, bool view = true, bool edit = true) => new(user, view, edit);

    /// <summary>Runs as <paramref name="user"/> (the query filters read the current user).</summary>
    private async Task<T> As<T>(string user, Func<DocumentService, Task<T>> act)
    {
        var previous = _currentUserId;
        _currentUserId = user;
        _db.ChangeTracker.Clear();
        try { return await act(Service()); }
        finally { _currentUserId = previous; _db.ChangeTracker.Clear(); }
    }

    private Task As(string user, Func<DocumentService, Task> act) => As<object?>(user, async s => { await act(s); return null; });

    private static IFormFile Pdf(string name = "evidence.pdf", int bytes = 128) =>
        new FormFile(new MemoryStream(Encoding.ASCII.GetBytes(new string('x', bytes))), 0, bytes, "file", name)
        {
            Headers = new HeaderDictionary(), ContentType = "application/pdf",
        };

    private async Task<ActionItem> AddItemAsync(ActionStatus status = ActionStatus.ToDo, bool isPrivate = false, bool deleted = false)
    {
        var item = new ActionItem
        {
            Id = Guid.NewGuid(), ActionId = "ACT-" + Guid.NewGuid().ToString("N")[..5], Title = "Item", Description = "",
            WorkspaceId = isPrivate ? _privateWorkspaceId : _workspaceId, Status = status, Priority = ActionPriority.Medium,
            DueDate = new DateTime(2030, 6, 28, 0, 0, 0, DateTimeKind.Utc), CreatedByUserId = Creator, CreatedAt = DateTime.UtcNow,
            IsDeleted = deleted,
        };
        item.Assignees.Add(new ActionItemAssignee { ActionItemId = item.Id, UserId = Assignee });
        _db.ActionItems.Add(item);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return item;
    }

    private async Task<ActionItemSubItem> AddSubItemAsync(Guid itemId, bool deleted = false)
    {
        var s = new ActionItemSubItem
        {
            Id = Guid.NewGuid(), ActionItemId = itemId, Title = "Sub", SortOrder = 1, IsDeleted = deleted,
            EndDate = new DateTime(2030, 6, 7, 0, 0, 0, DateTimeKind.Utc), CreatedAt = DateTime.UtcNow,
        };
        s.Assignees.Add(new ActionItemSubItemAssignee { SubItemId = s.Id, UserId = SubOwner });
        _db.ActionItemSubItems.Add(s);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return s;
    }

    private async Task<ActionItemUpdate> AddUpdateAsync(Guid itemId, string author, ActionItemUpdateStatus status, bool deleted = false)
    {
        var u = new ActionItemUpdate
        {
            Id = Guid.NewGuid(), ActionItemId = itemId, Content = "u", AuthorUserId = author, Status = status, IsDeleted = deleted,
            SubmittedAt = status == ActionItemUpdateStatus.Submitted ? DateTime.UtcNow : null,
        };
        _db.ActionItemUpdates.Add(u);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return u;
    }

    /// <summary>A document written straight to the database (as if uploaded earlier).</summary>
    private async Task<Document> AddDocAsync(string type, Guid parentId, string uploader)
    {
        var d = new Document
        {
            Id = Guid.NewGuid(), Name = "Doc", FileName = "doc.pdf", ContentType = "application/pdf", FileSize = 3,
            Content = new byte[] { 1, 2, 3 }, RelatedEntityType = type, RelatedEntityId = parentId,
            UploadedByUserId = uploader, CreatedAt = DateTime.UtcNow,
        };
        _db.Documents.Add(d);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return d;
    }

    private bool DocExists(Guid id)
    {
        _db.ChangeTracker.Clear();
        return _db.Documents.Any(d => d.Id == id);
    }

    // ── Types ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unsupported_entity_type_is_refused()
    {
        await FluentActions.Awaiting(() => As(Creator, s => s.GetByEntityAsync("Workspace", Guid.NewGuid(), Can(Creator), default)))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*not available for entity type 'Workspace'*");
        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("Anything", Guid.NewGuid(), "n", Pdf(), Can(Creator), default)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Type_names_are_case_insensitive_and_stored_canonically()
    {
        var item = await AddItemAsync();
        var dto = await As(Creator, s => s.UploadAsync("actionitem", item.Id, "Plan", Pdf(), Can(Creator), default));
        dto.RelatedEntityType.Should().Be("ActionItem");
    }

    // ── Hidden parents: 404 everywhere, also by document id ──────────────────

    [Theory]
    [InlineData("private")]
    [InlineData("deleted")]
    public async Task A_hidden_action_item_is_404_for_list_upload_download_and_delete(string how)
    {
        var item = await AddItemAsync(isPrivate: how == "private", deleted: how == "deleted");
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);
        var user = how == "private" ? Editor : Creator;   // Editor is not a member of the private workspace

        await FluentActions.Awaiting(() => As(user, s => s.GetByEntityAsync("ActionItem", item.Id, Can(user), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(user, s => s.UploadAsync("ActionItem", item.Id, "x", Pdf(), Can(user), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(user, s => s.DownloadAsync(doc.Id, Can(user), default)))
            .Should().ThrowAsync<KeyNotFoundException>().WithMessage($"Document {doc.Id} not found.");
        await FluentActions.Awaiting(() => As(user, s => s.DeleteAsync(doc.Id, Can(user), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        DocExists(doc.Id).Should().BeTrue();
    }

    [Fact]
    public async Task A_private_workspace_member_still_has_access()
    {
        var item = await AddItemAsync(isPrivate: true);
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);

        (await As(Creator, s => s.GetByEntityAsync("ActionItem", item.Id, Can(Creator), default))).Should().ContainSingle();
        (await As(Creator, s => s.DownloadAsync(doc.Id, Can(Creator), default))).Content.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Project_milestone_and_risk_files_follow_their_parent_visibility()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(), ProjectCode = "PRJ-1", Name = "P", WorkspaceId = _privateWorkspaceId,
            ProjectManagerUserId = Creator, PlannedStartDate = DateTime.UtcNow, PlannedEndDate = DateTime.UtcNow.AddDays(9),
        };
        var milestone = new Milestone
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, MilestoneCode = "MS-1", Name = "M",
            PlannedStartDate = DateTime.UtcNow, PlannedDueDate = DateTime.UtcNow.AddDays(3),
        };
        var risk = new ProjectRisk { Id = Guid.NewGuid(), ProjectId = project.Id, RiskCode = "RISK-1", Title = "R" };
        _db.Projects.Add(project);
        _db.Milestones.Add(milestone);
        _db.ProjectRisks.Add(risk);
        await _db.SaveChangesAsync();

        foreach (var (type, id) in new[] { ("Project", project.Id), ("Milestone", milestone.Id), ("ProjectRisk", risk.Id) })
        {
            var doc = await AddDocAsync(type, id, Creator);
            await FluentActions.Awaiting(() => As(Outsider, s => s.GetByEntityAsync(type, id, Can(Outsider, false, false), default)))
                .Should().ThrowAsync<KeyNotFoundException>(type);
            await FluentActions.Awaiting(() => As(Outsider, s => s.DownloadAsync(doc.Id, Can(Outsider, false, false), default)))
                .Should().ThrowAsync<KeyNotFoundException>(type);

            // Members: as before — no permission policy, and anyone who sees the parent may delete.
            var list = await As(Creator, s => s.GetByEntityAsync(type, id, Can(Creator, false, false), default));
            list.Should().ContainSingle().Which.CanDelete.Should().BeTrue();
            await As(Creator, s => s.DeleteAsync(doc.Id, Can(Creator, false, false), default));
            DocExists(doc.Id).Should().BeFalse(type);
        }
    }

    // ── Permissions on the action-item types ─────────────────────────────────

    [Fact]
    public async Task Action_item_files_need_View_to_read_and_Edit_to_write()
    {
        var item = await AddItemAsync();
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);

        await FluentActions.Awaiting(() => As(Editor, s => s.GetByEntityAsync("ActionItem", item.Id, Can(Editor, view: false), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => As(Editor, s => s.DownloadAsync(doc.Id, Can(Editor, view: false), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => As(Editor, s => s.UploadAsync("ActionItem", item.Id, "x", Pdf(), Can(Editor, edit: false), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => As(Creator, s => s.DeleteAsync(doc.Id, Can(Creator, edit: false), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>();

        var readOnly = await As(Creator, s => s.GetByEntityAsync("ActionItem", item.Id, Can(Creator, edit: false), default));
        readOnly.Should().ContainSingle().Which.CanDelete.Should().BeFalse("no Edit permission");
    }

    // ── Done / Cancelled (D1) ────────────────────────────────────────────────

    [Theory]
    [InlineData(ActionStatus.Done, "Done")]
    [InlineData(ActionStatus.Cancelled, "Cancelled")]
    public async Task A_Done_or_Cancelled_item_locks_its_files_and_its_sub_items_files(ActionStatus status, string label)
    {
        var item = await AddItemAsync(status);
        var sub = await AddSubItemAsync(item.Id);
        var itemDoc = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);
        var subDoc = await AddDocAsync(DocumentEntityTypes.ActionItemSubItem, sub.Id, Creator);

        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("ActionItem", item.Id, "x", Pdf(), Can(Creator), default)))
            .Should().ThrowAsync<DocumentLockedException>()
            .WithMessage($"Files cannot be added to or removed from an action item that is {label}. Add them to an action update instead.");
        await FluentActions.Awaiting(() => As(AdminId, s => s.DeleteAsync(itemDoc.Id, Can(AdminId), default)))
            .Should().ThrowAsync<DocumentLockedException>();
        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("ActionItemSubItem", sub.Id, "x", Pdf(), Can(Creator), default)))
            .Should().ThrowAsync<DocumentLockedException>().WithMessage($"*while the action item is {label}.");
        await FluentActions.Awaiting(() => As(Creator, s => s.DeleteAsync(subDoc.Id, Can(Creator), default)))
            .Should().ThrowAsync<DocumentLockedException>();

        // Reading stays possible, and the delete button is hidden.
        (await As(Creator, s => s.GetByEntityAsync("ActionItem", item.Id, Can(Creator), default)))
            .Should().ContainSingle().Which.CanDelete.Should().BeFalse();
        (await As(Creator, s => s.DownloadAsync(subDoc.Id, Can(Creator), default))).FileName.Should().Be("doc.pdf");

        // Updates on the Done item still take files.
        var draft = await AddUpdateAsync(item.Id, Creator, ActionItemUpdateStatus.Draft);
        (await As(Creator, s => s.UploadAsync("ActionItemUpdate", draft.Id, "After", Pdf(), Can(Creator), default)))
            .RelatedEntityType.Should().Be("ActionItemUpdate");
    }

    // ── Delete rights on item / sub-item files (D4) ──────────────────────────

    [Fact]
    public async Task Item_files_can_be_deleted_by_uploader_creator_assignees_or_Admin_only()
    {
        var item = await AddItemAsync();
        var byEditor = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Editor);
        var byCreator = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);
        var forAssignee = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);
        var forAdmin = await AddDocAsync(DocumentEntityTypes.ActionItem, item.Id, Creator);

        // canDelete as each user sees it
        async Task<Dictionary<Guid, bool>> Rights(string user) =>
            (await As(user, s => s.GetByEntityAsync("ActionItem", item.Id, Can(user), default))).ToDictionary(d => d.Id, d => d.CanDelete);
        (await Rights(Editor)).Should().BeEquivalentTo(new Dictionary<Guid, bool>
            { [byEditor.Id] = true, [byCreator.Id] = false, [forAssignee.Id] = false, [forAdmin.Id] = false });
        (await Rights(Creator)).Values.Should().OnlyContain(v => v);
        (await Rights(Assignee)).Values.Should().OnlyContain(v => v);
        (await Rights(AdminId)).Values.Should().OnlyContain(v => v);
        (await Rights(SubOwner)).Values.Should().OnlyContain(v => !v, "a sub-item assignee has no rights on the item's own files");

        // Another editor can upload but not delete someone else's file.
        await FluentActions.Awaiting(() => As(Editor, s => s.DeleteAsync(byCreator.Id, Can(Editor), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Only the person who uploaded this file*");
        (await As(Editor, s => s.UploadAsync("ActionItem", item.Id, "Mine", Pdf(), Can(Editor), default))).CanDelete.Should().BeTrue();

        await As(Editor, s => s.DeleteAsync(byEditor.Id, Can(Editor), default));
        await As(Creator, s => s.DeleteAsync(byCreator.Id, Can(Creator), default));
        await As(Assignee, s => s.DeleteAsync(forAssignee.Id, Can(Assignee), default));
        await As(AdminId, s => s.DeleteAsync(forAdmin.Id, Can(AdminId), default));
        new[] { byEditor.Id, byCreator.Id, forAssignee.Id, forAdmin.Id }.Should().OnlyContain(id => !DocExists(id));
    }

    [Fact]
    public async Task Sub_item_files_can_also_be_deleted_by_the_sub_items_assignees()
    {
        var item = await AddItemAsync();
        var sub = await AddSubItemAsync(item.Id);
        var a = await AddDocAsync(DocumentEntityTypes.ActionItemSubItem, sub.Id, Creator);
        var b = await AddDocAsync(DocumentEntityTypes.ActionItemSubItem, sub.Id, Creator);

        await FluentActions.Awaiting(() => As(Editor, s => s.DeleteAsync(a.Id, Can(Editor), default)))
            .Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*the sub-item's assignees*");
        (await As(SubOwner, s => s.GetByEntityAsync("ActionItemSubItem", sub.Id, Can(SubOwner), default)))
            .Should().OnlyContain(d => d.CanDelete);
        await As(SubOwner, s => s.DeleteAsync(a.Id, Can(SubOwner), default));
        await As(Assignee, s => s.DeleteAsync(b.Id, Can(Assignee), default));
        DocExists(a.Id).Should().BeFalse();
        DocExists(b.Id).Should().BeFalse();
    }

    [Fact]
    public async Task A_deleted_sub_item_or_one_in_a_private_workspace_is_404()
    {
        var item = await AddItemAsync();
        var deleted = await AddSubItemAsync(item.Id, deleted: true);
        var privateItem = await AddItemAsync(isPrivate: true);
        var privateSub = await AddSubItemAsync(privateItem.Id);
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItemSubItem, privateSub.Id, Creator);

        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("ActionItemSubItem", deleted.Id, "x", Pdf(), Can(Creator), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Editor, s => s.DownloadAsync(doc.Id, Can(Editor), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Action updates ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_updates_files_belong_to_its_author_and_are_invisible_to_everyone_else()
    {
        var item = await AddItemAsync();
        var draft = await AddUpdateAsync(item.Id, Editor, ActionItemUpdateStatus.Draft);

        var uploaded = await As(Editor, s => s.UploadAsync("ActionItemUpdate", draft.Id, "Draft file", Pdf(), Can(Editor), default));
        uploaded.CanDelete.Should().BeTrue();

        foreach (var other in new[] { Creator, Assignee, AdminId })
        {
            await FluentActions.Awaiting(() => As(other, s => s.GetByEntityAsync("ActionItemUpdate", draft.Id, Can(other), default)))
                .Should().ThrowAsync<KeyNotFoundException>(other);
            await FluentActions.Awaiting(() => As(other, s => s.DownloadAsync(uploaded.Id, Can(other), default)))
                .Should().ThrowAsync<KeyNotFoundException>(other);
            await FluentActions.Awaiting(() => As(other, s => s.UploadAsync("ActionItemUpdate", draft.Id, "x", Pdf(), Can(other), default)))
                .Should().ThrowAsync<KeyNotFoundException>(other);
            await FluentActions.Awaiting(() => As(other, s => s.DeleteAsync(uploaded.Id, Can(other), default)))
                .Should().ThrowAsync<KeyNotFoundException>(other);
        }

        await As(Editor, s => s.DeleteAsync(uploaded.Id, Can(Editor), default));
        DocExists(uploaded.Id).Should().BeFalse();
    }

    [Fact]
    public async Task A_submitted_updates_files_are_locked_for_everyone_including_Admin()
    {
        var item = await AddItemAsync();
        var update = await AddUpdateAsync(item.Id, Editor, ActionItemUpdateStatus.Submitted);
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItemUpdate, update.Id, Editor);

        foreach (var user in new[] { Editor, Creator, AdminId })
        {
            await FluentActions.Awaiting(() => As(user, s => s.UploadAsync("ActionItemUpdate", update.Id, "x", Pdf(), Can(user), default)))
                .Should().ThrowAsync<DocumentLockedException>().WithMessage("This action update has been submitted. Its files cannot be changed.");
            await FluentActions.Awaiting(() => As(user, s => s.DeleteAsync(doc.Id, Can(user), default)))
                .Should().ThrowAsync<DocumentLockedException>();
            (await As(user, s => s.GetByEntityAsync("ActionItemUpdate", update.Id, Can(user), default)))
                .Should().ContainSingle().Which.CanDelete.Should().BeFalse();
        }
        (await As(Outsider, s => s.DownloadAsync(doc.Id, Can(Outsider), default))).Content.Should().Equal(1, 2, 3);
        DocExists(doc.Id).Should().BeTrue();
    }

    [Fact]
    public async Task A_deleted_drafts_files_stay_in_the_database_but_cannot_be_reached()
    {
        var item = await AddItemAsync();
        var draft = await AddUpdateAsync(item.Id, Editor, ActionItemUpdateStatus.Draft, deleted: true);
        var doc = await AddDocAsync(DocumentEntityTypes.ActionItemUpdate, draft.Id, Editor);

        await FluentActions.Awaiting(() => As(Editor, s => s.GetByEntityAsync("ActionItemUpdate", draft.Id, Can(Editor), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(Editor, s => s.DownloadAsync(doc.Id, Can(Editor), default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        DocExists(doc.Id).Should().BeTrue("D2: kept, hidden");
    }

    // ── Caps (Q13) ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ActionItem", "action item")]
    [InlineData("ActionItemSubItem", "sub-item")]
    [InlineData("ActionItemUpdate", "action update")]
    public async Task At_most_ten_files_per_record(string type, string label)
    {
        var item = await AddItemAsync();
        var parentId = type switch
        {
            "ActionItem"        => item.Id,
            "ActionItemSubItem" => (await AddSubItemAsync(item.Id)).Id,
            _                   => (await AddUpdateAsync(item.Id, Creator, ActionItemUpdateStatus.Draft)).Id,
        };
        for (var i = 0; i < 10; i++)
            await As(Creator, s => s.UploadAsync(type, parentId, $"f{i}", Pdf(), Can(Creator), default));

        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync(type, parentId, "f10", Pdf(), Can(Creator), default)))
            .Should().ThrowAsync<ArgumentException>().WithMessage($"Maximum of 10 files per {label} reached.*");
    }

    [Fact]
    public async Task File_rules_are_unchanged()
    {
        var item = await AddItemAsync();
        var big = Pdf(bytes: 10 * 1024 * 1024 + 1);
        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("ActionItem", item.Id, "big", big, Can(Creator), default)))
            .Should().ThrowAsync<ArgumentException>().WithMessage("File size exceeds the 10 MB limit.");
        var exe = new FormFile(new MemoryStream(new byte[] { 1 }), 0, 1, "file", "run.exe") { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        await FluentActions.Awaiting(() => As(Creator, s => s.UploadAsync("ActionItem", item.Id, "exe", exe, Can(Creator), default)))
            .Should().ThrowAsync<ArgumentException>().WithMessage("File type '.exe' is not allowed*");
    }

    // ── KPI evidence (D3) ────────────────────────────────────────────────────

    private async Task<(Guid TargetId, Guid DraftTargetId, Guid ActualId, Guid OrgUnitId)> AddKpiAsync()
    {
        var orgUnitId = Guid.NewGuid();
        _db.OrgUnits.Add(new OrgUnit { Id = orgUnitId, Name = "Unit", Level = 3, CreatedAt = DateTime.UtcNow });
        var objective = new StrategicObjective { Id = Guid.NewGuid(), ObjectiveCode = "SO-001", Statement = "S", OrgUnitId = orgUnitId, CreatedAt = DateTime.UtcNow };
        var kpi = new Kpi { Id = Guid.NewGuid(), Name = "K", StrategicObjectiveId = objective.Id, CreatedAt = DateTime.UtcNow };
        var target = new KpiTarget { Id = Guid.NewGuid(), KpiId = kpi.Id, Year = 2030, Month = 1 };
        var draftTarget = new KpiTarget { Id = Guid.NewGuid(), KpiId = kpi.Id, Year = 2030, Month = 2, IsDraft = true };
        var actual = new KpiActual { Id = Guid.NewGuid(), KpiTargetId = target.Id };
        _db.StrategicObjectives.Add(objective);
        _db.Kpis.Add(kpi);
        _db.KpiTargets.AddRange(target, draftTarget);
        _db.KpiActuals.Add(actual);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return (target.Id, draftTarget.Id, actual.Id, orgUnitId);
    }

    [Fact]
    public async Task KPI_evidence_out_of_the_read_scope_is_404_and_writes_keep_their_403()
    {
        var (targetId, draftTargetId, actualId, _) = await AddKpiAsync();
        var doc = await AddDocAsync(DocumentEntityTypes.KpiTarget, targetId, Creator);
        const string strategyEditor = "user-se";
        _scope.Setup(s => s.GetVisibleOrgUnitIdsAsync(strategyEditor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });      // some other subtree
        _scope.Setup(s => s.EnsureCanWriteAsync(strategyEditor, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("You are not authorised to manage strategic items in this org unit."));

        var caller = DocumentCaller.WithoutActionItemRights(strategyEditor);
        await FluentActions.Awaiting(() => As(strategyEditor, s => s.GetByEntityAsync("KpiTarget", targetId, caller, default)))
            .Should().ThrowAsync<KeyNotFoundException>();
        await FluentActions.Awaiting(() => As(strategyEditor, s => s.DownloadAsync(doc.Id, caller, default)))
            .Should().ThrowAsync<KeyNotFoundException>();

        // Everyone else (unrestricted scope): drafts count, actuals too, writes as before.
        var anyone = DocumentCaller.WithoutActionItemRights(Creator);
        (await As(Creator, s => s.GetByEntityAsync("KpiTarget", targetId, anyone, default))).Should().ContainSingle();
        (await As(Creator, s => s.UploadAsync("KpiTarget", draftTargetId, "draft evidence", Pdf(), anyone, default))).Should().NotBeNull();
        (await As(Creator, s => s.UploadAsync("KpiActual", actualId, "actual evidence", Pdf(), anyone, default))).CanDelete.Should().BeTrue();
        await FluentActions.Awaiting(() => As(Creator, s => s.GetByEntityAsync("KpiTarget", Guid.NewGuid(), anyone, default)))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task KPI_writes_by_an_in_read_scope_Strategy_Editor_still_go_through_the_org_unit_check()
    {
        var (targetId, _, _, orgUnitId) = await AddKpiAsync();
        const string strategyEditor = "user-se2";
        _scope.Setup(s => s.GetVisibleOrgUnitIdsAsync(strategyEditor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { orgUnitId });

        await As(strategyEditor, s => s.UploadAsync("KpiTarget", targetId, "ok", Pdf(), DocumentCaller.WithoutActionItemRights(strategyEditor), default));
        _scope.Verify(s => s.EnsureCanWriteAsync(strategyEditor, orgUnitId, It.IsAny<CancellationToken>()), Times.Once);

        for (var i = 1; i < 10; i++)
            await As(strategyEditor, s => s.UploadAsync("KpiTarget", targetId, $"f{i}", Pdf(), DocumentCaller.WithoutActionItemRights(strategyEditor), default));
        await FluentActions.Awaiting(() => As(strategyEditor, s => s.UploadAsync("KpiTarget", targetId, "f10", Pdf(), DocumentCaller.WithoutActionItemRights(strategyEditor), default)))
            .Should().ThrowAsync<ArgumentException>().WithMessage("Maximum of 10 files per KpiTarget reached.*", "the KPI cap message is unchanged");
    }
}
