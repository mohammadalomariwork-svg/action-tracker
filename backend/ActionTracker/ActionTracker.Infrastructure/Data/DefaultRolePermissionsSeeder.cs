using ActionTracker.Application.Permissions;
using ActionTracker.Domain.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Seeds the default role permissions for all eight system roles.
/// Idempotent — only inserts records that do not already exist
/// (checked by RoleName + AreaId + ActionId combination).
/// Must run AFTER PermissionCatalogSeeder.
/// </summary>
public static class DefaultRolePermissionsSeeder
{
    // OrgUnitScope int values: 0 = All, 1 = SpecificOrgUnit, 2 = OwnOnly
    private const int ScopeAll     = 0;
    private const int ScopeOwnOnly = 2;

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        // ── Collect all desired (roleName, areaId, areaName, actionId, actionName, scope) ─
        var desired = BuildDesiredPermissions().ToList();

        // ── Ensure Admin rows always have OrgUnitScope = 0 (All) ──────────────
        var adminRows = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(r => r.RoleName == AppRoles.Admin)
            .ToListAsync();

        int updatedCount = 0;
        foreach (var row in adminRows.Where(r => r.OrgUnitScope != ScopeAll))
        {
            row.OrgUnitScope = ScopeAll;
            row.IsActive     = true;
            row.IsDeleted    = false;
            updatedCount++;
        }

        if (updatedCount > 0)
            await db.SaveChangesAsync();

        // ── Load existing (roleName, areaId, actionId) keys ──────────────────
        var existingKeys = await db.RolePermissions
            .IgnoreQueryFilters()
            .Select(r => new RolePermissionKey(r.RoleName, r.AreaId, r.ActionId))
            .ToListAsync();

        var existingSet = existingKeys.ToHashSet();

        // ── Build missing rows ────────────────────────────────────────────────
        var now      = DateTime.UtcNow;
        var toInsert = new List<RolePermission>();

        foreach (var (roleName, areaId, areaName, actionId, actionName, scope) in desired)
        {
            var key = new RolePermissionKey(roleName, areaId, actionId);
            if (existingSet.Contains(key)) continue;

            toInsert.Add(new RolePermission
            {
                Id         = Guid.NewGuid(),
                RoleName   = roleName,
                AreaId     = areaId,
                AreaName   = areaName,
                ActionId   = actionId,
                ActionName = actionName,
                OrgUnitScope = scope,
                IsActive   = true,
                IsDeleted  = false,
                CreatedAt  = now,
                CreatedBy  = "system",
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

    private static IEnumerable<(string Role, Guid AreaId, string AreaName, Guid ActionId, string ActionName, int Scope)>
        BuildDesiredPermissions()
    {
        // ── Convenience aliases ───────────────────────────────────────────────
        // Areas
        var dash    = (PermissionCatalogSeeder.AreaDashboard,             "Dashboard");
        var ws      = (PermissionCatalogSeeder.AreaWorkspaces,            "Workspaces");
        var proj    = (PermissionCatalogSeeder.AreaProjects,              "Projects");
        var mile    = (PermissionCatalogSeeder.AreaMilestones,            "Milestones");
        var ai      = (PermissionCatalogSeeder.AreaActionItems,           "ActionItems");
        var so      = (PermissionCatalogSeeder.AreaStrategicObjectives,   "StrategicObjectives");
        var kpi     = (PermissionCatalogSeeder.AreaKPIs,                  "KPIs");
        var rep     = (PermissionCatalogSeeder.AreaReports,               "Reports");
        var org     = (PermissionCatalogSeeder.AreaOrgChart,              "OrgChart");
        var um      = (PermissionCatalogSeeder.AreaUserManagement,        "UserManagement");
        var pm      = (PermissionCatalogSeeder.AreaPermissionsManagement, "PermissionsManagement");
        var roles   = (PermissionCatalogSeeder.AreaRoles,                 "Roles");

        // Actions
        var view    = (PermissionCatalogSeeder.ActionView,    "View");
        var create  = (PermissionCatalogSeeder.ActionCreate,  "Create");
        var edit    = (PermissionCatalogSeeder.ActionEdit,    "Edit");
        var delete  = (PermissionCatalogSeeder.ActionDelete,  "Delete");
        var approve = (PermissionCatalogSeeder.ActionApprove, "Approve");
        var export  = (PermissionCatalogSeeder.ActionExport,  "Export");
        var assign  = (PermissionCatalogSeeder.ActionAssign,  "Assign");

        var allActions = new[] { view, create, edit, delete, approve, export, assign };

        // Local helper
        static (string, Guid, string, Guid, string, int) P(
            string role,
            (Guid Id, string Name) area,
            (Guid Id, string Name) action,
            int scope = ScopeAll)
            => (role, area.Id, area.Name, action.Id, action.Name, scope);

        // ── Admin — every area × every action, scope All ──────────────────────
        var allAreas = new[] { dash, ws, proj, mile, ai, so, kpi, rep, org, um, pm, roles };
        foreach (var area   in allAreas)
        foreach (var action in allActions)
            yield return P(AppRoles.Admin, area, action);

        // ── PMO Head — full on 10 areas, View on PermissionsManagement + Roles ─
        var pmoFullAreas = new[] { dash, proj, mile, ai, so, kpi, rep, ws, org, um };
        foreach (var area   in pmoFullAreas)
        foreach (var action in allActions)
            yield return P(AppRoles.PmoHead, area, action);
        yield return P(AppRoles.PmoHead, pm,    view);
        yield return P(AppRoles.PmoHead, roles, view);

        // ── PMO Analyst — View+Create+Edit on 6 areas, View-only on 3 ─────────
        var analyRich = new[] { proj, mile, ai, so, kpi, rep };
        var vce       = new[] { view, create, edit };
        foreach (var area   in analyRich)
        foreach (var action in vce)
            yield return P(AppRoles.PmoAnalyst, area, action);
        foreach (var area in new[] { dash, ws, org })
            yield return P(AppRoles.PmoAnalyst, area, view);

        // ── Project Sponsor — View+Approve on 5 areas, View-only on 2 ─────────
        var sponsorRich = new[] { proj, mile, ai, rep, kpi };
        foreach (var area in sponsorRich)
        {
            yield return P(AppRoles.ProjectSponsor, area, view);
            yield return P(AppRoles.ProjectSponsor, area, approve);
        }
        foreach (var area in new[] { dash, so })
            yield return P(AppRoles.ProjectSponsor, area, view);

        // ── Project Manager ───────────────────────────────────────────────────
        var pmRich   = new[] { proj, mile, rep };
        var vcdeExp  = new[] { view, create, edit, delete, export };
        foreach (var area   in pmRich)
        foreach (var action in vcdeExp)
            yield return P(AppRoles.ProjectManager, area, action);

        // ActionItems: View+Create+Edit+Delete+Export+Assign
        foreach (var action in new[] { view, create, edit, delete, export, assign })
            yield return P(AppRoles.ProjectManager, ai, action);

        foreach (var area in new[] { dash, kpi, so })
            yield return P(AppRoles.ProjectManager, area, view);

        // ── Project Coordinator ───────────────────────────────────────────────
        foreach (var area   in new[] { ai, mile })
        foreach (var action in vce)
            yield return P(AppRoles.ProjectCoordinator, area, action);
        foreach (var area in new[] { proj, dash, rep })
            yield return P(AppRoles.ProjectCoordinator, area, view);

        // ── Team Member — View+Edit on ActionItems (OwnOnly), View-only on 3 ──
        yield return P(AppRoles.TeamMember, ai, view, ScopeOwnOnly);
        yield return P(AppRoles.TeamMember, ai, edit, ScopeOwnOnly);
        foreach (var area in new[] { proj, mile, dash })
            yield return P(AppRoles.TeamMember, area, view);

        // ── Workspace Admin — full on 3 areas, View-only on 4 ────────────────
        foreach (var area   in new[] { ws, org, um })
        foreach (var action in allActions)
            yield return P(AppRoles.WorkspaceAdmin, area, action);
        foreach (var area in new[] { dash, proj, ai, rep })
            yield return P(AppRoles.WorkspaceAdmin, area, view);
        yield return P(AppRoles.WorkspaceAdmin, roles, view);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper — value-equality key for duplicate detection
    // ─────────────────────────────────────────────────────────────────────────

    private readonly record struct RolePermissionKey(
        string RoleName,
        Guid   AreaId,
        Guid   ActionId);
}
