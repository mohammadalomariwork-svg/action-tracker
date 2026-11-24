using ActionTracker.Application.Permissions;
using ActionTracker.Domain.Constants;
using ActionTracker.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// <see cref="DefaultRolePermissionsSeeder"/> runs at every startup. It only
/// inserts (role, area, action) keys that have no row at all — in effect,
/// inactive or soft-deleted — so it never duplicates a grant, never touches an
/// existing row and never restores a grant an admin removed (the role UI
/// soft-deletes). WO-2026-11 A-1b added Team Member → ActionItems.Create, which
/// production already had, because POST /api/action-items now requires it.
/// </summary>
public class DefaultRolePermissionsSeederTests : IDisposable
{
    private static readonly Guid ActionItems = PermissionCatalogSeeder.AreaActionItems;
    private static readonly Guid Create      = PermissionCatalogSeeder.ActionCreate;

    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => _db.Dispose();

    private Task SeedAsync() => DefaultRolePermissionsSeeder.SeedAsync(_db, NullLogger.Instance);

    private Task<List<RolePermission>> RowsAsync(string role, Guid areaId, Guid actionId) =>
        _db.RolePermissions.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.RoleName == role && r.AreaId == areaId && r.ActionId == actionId)
            .ToListAsync();

    private async Task<RolePermission> AddRowAsync(string role, Guid areaId, Guid actionId, bool isActive, bool isDeleted)
    {
        var row = new RolePermission
        {
            Id = Guid.NewGuid(), RoleName = role, AreaId = areaId, AreaName = "ActionItems",
            ActionId = actionId, ActionName = "Create", IsActive = isActive, IsDeleted = isDeleted,
            CreatedAt = new DateTime(2026, 5, 18, 6, 13, 52, DateTimeKind.Utc), CreatedBy = "admin-user",
        };
        _db.RolePermissions.Add(row);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return row;
    }

    [Fact]
    public async Task FreshDatabase_GivesTeamMemberViewCreateAndEditOnActionItems()
    {
        await SeedAsync();

        var actions = await _db.RolePermissions.AsNoTracking()
            .Where(r => r.RoleName == AppRoles.TeamMember && r.AreaId == ActionItems && r.IsActive)
            .Select(r => r.ActionName)
            .ToListAsync();

        actions.Should().BeEquivalentTo("View", "Create", "Edit");
    }

    [Fact]
    public async Task ARowAlreadyInEffect_IsKeptAsItIs_AndNotDuplicated()
    {
        // Production: Team Member → ActionItems.Create was added through the role UI.
        var existing = await AddRowAsync(AppRoles.TeamMember, ActionItems, Create, isActive: true, isDeleted: false);

        await SeedAsync();

        var rows = await RowsAsync(AppRoles.TeamMember, ActionItems, Create);
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(existing.Id);
        rows[0].CreatedBy.Should().Be("admin-user");
    }

    [Fact]
    public async Task ARemovedGrant_IsNotRestored()
    {
        // The role UI soft-deletes a grant it removes; the seeder must respect that.
        await AddRowAsync(AppRoles.TeamMember, ActionItems, Create, isActive: false, isDeleted: true);

        await SeedAsync();

        var rows = await RowsAsync(AppRoles.TeamMember, ActionItems, Create);
        rows.Should().ContainSingle().Which.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task AnInactiveGrant_IsLeftInactive()
    {
        await AddRowAsync(AppRoles.TeamMember, ActionItems, Create, isActive: false, isDeleted: false);

        await SeedAsync();

        var rows = await RowsAsync(AppRoles.TeamMember, ActionItems, Create);
        rows.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ASecondRun_InsertsNothing()
    {
        await SeedAsync();
        var first = await _db.RolePermissions.IgnoreQueryFilters().CountAsync();

        await SeedAsync();

        (await _db.RolePermissions.IgnoreQueryFilters().CountAsync()).Should().Be(first);
    }
}
