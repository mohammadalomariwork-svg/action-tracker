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
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        // ── Collect all desired (roleName, areaId, areaName, actionId, actionName) ─
        var desired = BuildDesiredPermissions().ToList();

        // ── Load existing (roleName, areaId, actionId) keys ──────────────────
        var existingKeys = await db.RolePermissions
            .IgnoreQueryFilters()
            .Select(r => new RolePermissionKey(r.RoleName, r.AreaId, r.ActionId))
            .ToListAsync();

        var existingSet = existingKeys.ToHashSet();

        // ── Build missing rows ────────────────────────────────────────────────
        var now      = DateTime.UtcNow;
        var toInsert = new List<RolePermission>();

        foreach (var (roleName, areaId, areaName, actionId, actionName) in desired)
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
                IsActive   = true,
                IsDeleted  = false,
                CreatedAt  = now,
                CreatedBy  = "system",
            });
        }

        if (toInsert.Count == 0)
        {
            logger.LogInformation("DefaultRolePermissionsSeeder: all permissions already seeded, nothing to insert.");
            return;
        }

        await db.RolePermissions.AddRangeAsync(toInsert);
        await db.SaveChangesAsync();

        logger.LogInformation(
            "DefaultRolePermissionsSeeder: inserted {Count} role permission(s).",
            toInsert.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Permission matrix
    // ─────────────────────────────────────────────────────────────────────────

    private static IEnumerable<(string Role, Guid AreaId, string AreaName, Guid ActionId, string ActionName)>
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
        var risks   = (PermissionCatalogSeeder.AreaRisks,                 "Risks");
        var emailT  = (PermissionCatalogSeeder.AreaEmailTemplates,        "EmailTemplates");
        var notif   = (PermissionCatalogSeeder.AreaNotifications,         "Notifications");

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
        static (string, Guid, string, Guid, string) P(
            string role,
            (Guid Id, string Name) area,
            (Guid Id, string Name) action)
            => (role, area.Id, area.Name, action.Id, action.Name);

        // ── Admin — every area × every action ─────────────────────────────────
        var allAreas = new[] { dash, ws, proj, mile, ai, so, kpi, rep, org, um, pm, roles, risks, emailT, notif };
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

        // ── Team Member — View+Edit on ActionItems, View-only on 3 ───────────
        yield return P(AppRoles.TeamMember, ai, view);
        yield return P(AppRoles.TeamMember, ai, edit);
        foreach (var area in new[] { proj, mile, dash })
            yield return P(AppRoles.TeamMember, area, view);

        // ── Workspace Admin — full on 3 areas, View-only on 4 ────────────────
        foreach (var area   in new[] { ws, org, um })
        foreach (var action in allActions)
            yield return P(AppRoles.WorkspaceAdmin, area, action);
        foreach (var area in new[] { dash, proj, ai, rep })
            yield return P(AppRoles.WorkspaceAdmin, area, view);
        yield return P(AppRoles.WorkspaceAdmin, roles, view);

        // ══════════════════════════════════════════════════════════════════════
        // New areas: Risks, EmailTemplates, Notifications
        // ══════════════════════════════════════════════════════════════════════

        // Manager (PMO Head, PMO Analyst, Project Manager, Project Coordinator)
        // → Risks: View, Create, Edit, Export
        foreach (var role in new[] { AppRoles.PmoHead, AppRoles.PmoAnalyst, AppRoles.ProjectManager })
        foreach (var action in new[] { view, create, edit, export })
            yield return P(role, risks, action);

        // → Notifications: View, Delete (for Manager-tier roles)
        foreach (var role in new[] { AppRoles.PmoHead, AppRoles.PmoAnalyst, AppRoles.ProjectManager, AppRoles.ProjectCoordinator })
        foreach (var action in new[] { view, delete })
            yield return P(role, notif, action);

        // Project Sponsor — Risks: View
        yield return P(AppRoles.ProjectSponsor, risks, view);
        yield return P(AppRoles.ProjectSponsor, notif, view);
        yield return P(AppRoles.ProjectSponsor, notif, delete);

        // Project Coordinator — Risks: View
        yield return P(AppRoles.ProjectCoordinator, risks, view);

        // Team Member — Risks: View; Notifications: View, Delete
        yield return P(AppRoles.TeamMember, risks, view);
        yield return P(AppRoles.TeamMember, notif, view);
        yield return P(AppRoles.TeamMember, notif, delete);

        // Workspace Admin — Notifications: View, Delete
        yield return P(AppRoles.WorkspaceAdmin, notif, view);
        yield return P(AppRoles.WorkspaceAdmin, notif, delete);

        // Viewer — Risks: View; Notifications: View
        yield return P("Viewer", risks, view);
        yield return P("Viewer", notif, view);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper — value-equality key for duplicate detection
    // ─────────────────────────────────────────────────────────────────────────

    private readonly record struct RolePermissionKey(
        string RoleName,
        Guid   AreaId,
        Guid   ActionId);
}
