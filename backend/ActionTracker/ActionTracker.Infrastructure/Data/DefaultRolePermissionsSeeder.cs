using ActionTracker.Application.Permissions;
using ActionTracker.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Seeds the default role permissions for all eight system roles.
/// Idempotent — only inserts records that do not already exist
/// (checked by RoleName + Area + Action combination).
/// </summary>
public static class DefaultRolePermissionsSeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        // ── Collect all desired (roleName, area, action, scope) tuples ────────
        var desired = BuildDesiredPermissions();

        // ── Load existing Admin rows for scope correction ─────────────────────
        // Admin permissions must always have OrgUnitScope.All regardless of what
        // is currently stored; fix any mismatches before the insert pass.
        var adminRows = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(r => r.RoleName == AppRoles.Admin)
            .ToListAsync();

        int updatedCount = 0;
        foreach (var row in adminRows.Where(r => r.OrgUnitScope != OrgUnitScope.All))
        {
            row.OrgUnitScope = OrgUnitScope.All;
            row.IsActive     = true;
            row.IsDeleted    = false;
            updatedCount++;
        }

        if (updatedCount > 0)
            await db.SaveChangesAsync();

        // ── Load existing (roleName, area, action) keys in one query ─────────
        // Ignore global query filter so soft-deleted rows are also considered.
        var existingKeys = await db.RolePermissions
            .IgnoreQueryFilters()
            .Select(r => new RolePermissionKey(r.RoleName, r.Area, r.Action))
            .ToListAsync();

        var existingSet = existingKeys.ToHashSet();

        // ── Build the missing rows ────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var toInsert = new List<RolePermission>();

        foreach (var (roleName, area, action, scope) in desired)
        {
            var key = new RolePermissionKey(roleName, area, action);
            if (existingSet.Contains(key)) continue;

            toInsert.Add(new RolePermission
            {
                Id           = Guid.NewGuid(),
                RoleName     = roleName,
                Area         = area,
                Action       = action,
                OrgUnitScope = scope,
                IsActive     = true,
                IsDeleted    = false,
                CreatedAt    = now,
                CreatedBy    = "system",
            });
        }

        if (toInsert.Count == 0 && updatedCount == 0)
        {
            logger.LogInformation("DefaultRolePermissionsSeeder: all permissions already seeded, nothing to insert.");
            return;
        }

        if (toInsert.Count > 0)
            await db.RolePermissions.AddRangeAsync(toInsert);

        await db.SaveChangesAsync();

        logger.LogInformation(
            "DefaultRolePermissionsSeeder: inserted {Count} and corrected {Updated} role permission(s).",
            toInsert.Count, updatedCount);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Permission matrix
    // ─────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(string Role, PermissionArea Area, PermissionAction Action, OrgUnitScope Scope)>
        BuildDesiredPermissions()
    {
        var all = OrgUnitScope.All;

        // ── Admin — every area × every action ────────────────────────────────
        foreach (var area in Enum.GetValues<PermissionArea>())
        foreach (var action in Enum.GetValues<PermissionAction>())
            yield return (AppRoles.Admin, area, action, all);

        // ── PMO Head — full on 10 areas, View-only on PermissionsManagement ──
        var pmoHeadFullAreas = new[]
        {
            PermissionArea.Dashboard, PermissionArea.Projects,  PermissionArea.Milestones,
            PermissionArea.ActionItems, PermissionArea.StrategicObjectives, PermissionArea.KPIs,
            PermissionArea.Reports, PermissionArea.Workspaces, PermissionArea.OrgChart,
            PermissionArea.UserManagement,
        };
        foreach (var area in pmoHeadFullAreas)
        foreach (var action in Enum.GetValues<PermissionAction>())
            yield return (AppRoles.PmoHead, area, action, all);
        yield return (AppRoles.PmoHead, PermissionArea.PermissionsManagement, PermissionAction.View, all);

        // ── PMO Analyst — View+Create+Edit on 6 areas, View-only on 3 ────────
        var pmoAnalystRichAreas = new[]
        {
            PermissionArea.Projects, PermissionArea.Milestones, PermissionArea.ActionItems,
            PermissionArea.StrategicObjectives, PermissionArea.KPIs, PermissionArea.Reports,
        };
        var vce = new[] { PermissionAction.View, PermissionAction.Create, PermissionAction.Edit };
        foreach (var area in pmoAnalystRichAreas)
        foreach (var action in vce)
            yield return (AppRoles.PmoAnalyst, area, action, all);

        foreach (var area in new[] { PermissionArea.Dashboard, PermissionArea.Workspaces, PermissionArea.OrgChart })
            yield return (AppRoles.PmoAnalyst, area, PermissionAction.View, all);

        // ── Project Sponsor — View+Approve on 5 areas, View-only on 2 ────────
        var sponsorRichAreas = new[]
        {
            PermissionArea.Projects, PermissionArea.Milestones, PermissionArea.ActionItems,
            PermissionArea.Reports, PermissionArea.KPIs,
        };
        foreach (var area in sponsorRichAreas)
        {
            yield return (AppRoles.ProjectSponsor, area, PermissionAction.View, all);
            yield return (AppRoles.ProjectSponsor, area, PermissionAction.Approve, all);
        }
        foreach (var area in new[] { PermissionArea.Dashboard, PermissionArea.StrategicObjectives })
            yield return (AppRoles.ProjectSponsor, area, PermissionAction.View, all);

        // ── Project Manager ───────────────────────────────────────────────────
        // View+Create+Edit+Delete+Export on Projects, Milestones, Reports
        var pmRichAreas = new[] { PermissionArea.Projects, PermissionArea.Milestones, PermissionArea.Reports };
        var vcdeExp = new[]
        {
            PermissionAction.View, PermissionAction.Create, PermissionAction.Edit,
            PermissionAction.Delete, PermissionAction.Export,
        };
        foreach (var area in pmRichAreas)
        foreach (var action in vcdeExp)
            yield return (AppRoles.ProjectManager, area, action, all);

        // ActionItems: View+Create+Edit+Delete+Export+Assign
        foreach (var action in new[]
        {
            PermissionAction.View, PermissionAction.Create, PermissionAction.Edit,
            PermissionAction.Delete, PermissionAction.Export, PermissionAction.Assign,
        })
            yield return (AppRoles.ProjectManager, PermissionArea.ActionItems, action, all);

        // View-only for Dashboard, KPIs, StrategicObjectives
        foreach (var area in new[] { PermissionArea.Dashboard, PermissionArea.KPIs, PermissionArea.StrategicObjectives })
            yield return (AppRoles.ProjectManager, area, PermissionAction.View, all);

        // ── Project Coordinator — View+Create+Edit on ActionItems+Milestones, View-only on 3 ──
        foreach (var area in new[] { PermissionArea.ActionItems, PermissionArea.Milestones })
        foreach (var action in vce)
            yield return (AppRoles.ProjectCoordinator, area, action, all);

        foreach (var area in new[] { PermissionArea.Projects, PermissionArea.Dashboard, PermissionArea.Reports })
            yield return (AppRoles.ProjectCoordinator, area, PermissionAction.View, all);

        // ── Team Member — View+Edit on ActionItems (OwnOnly), View-only on 3 ─
        yield return (AppRoles.TeamMember, PermissionArea.ActionItems, PermissionAction.View, OrgUnitScope.OwnOnly);
        yield return (AppRoles.TeamMember, PermissionArea.ActionItems, PermissionAction.Edit, OrgUnitScope.OwnOnly);

        foreach (var area in new[] { PermissionArea.Projects, PermissionArea.Milestones, PermissionArea.Dashboard })
            yield return (AppRoles.TeamMember, area, PermissionAction.View, all);

        // ── Workspace Admin — full on 3 areas, View-only on 4 ────────────────
        foreach (var area in new[] { PermissionArea.Workspaces, PermissionArea.OrgChart, PermissionArea.UserManagement })
        foreach (var action in Enum.GetValues<PermissionAction>())
            yield return (AppRoles.WorkspaceAdmin, area, action, all);

        foreach (var area in new[]
        {
            PermissionArea.Dashboard, PermissionArea.Projects,
            PermissionArea.ActionItems, PermissionArea.Reports,
        })
            yield return (AppRoles.WorkspaceAdmin, area, PermissionAction.View, all);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper — value-equality key for duplicate detection
    // ─────────────────────────────────────────────────────────────────────────

    private readonly record struct RolePermissionKey(
        string RoleName,
        PermissionArea Area,
        PermissionAction Action);
}
