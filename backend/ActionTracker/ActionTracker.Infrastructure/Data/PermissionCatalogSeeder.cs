using ActionTracker.Application.Permissions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Seeds AppPermissionArea, AppPermissionAction, and AreaPermissionMapping tables.
/// Idempotent — inserts only records not already present (checked by hardcoded Guid).
/// Must run BEFORE DefaultRolePermissionsSeeder.
/// </summary>
public static class PermissionCatalogSeeder
{
    // ── Area IDs (stable across re-runs) ──────────────────────────────────────

    public static readonly Guid AreaDashboard               = new("10000001-0000-0000-0000-000000000000");
    public static readonly Guid AreaWorkspaces              = new("10000002-0000-0000-0000-000000000000");
    public static readonly Guid AreaProjects                = new("10000003-0000-0000-0000-000000000000");
    public static readonly Guid AreaMilestones              = new("10000004-0000-0000-0000-000000000000");
    public static readonly Guid AreaActionItems             = new("10000005-0000-0000-0000-000000000000");
    public static readonly Guid AreaStrategicObjectives     = new("10000006-0000-0000-0000-000000000000");
    public static readonly Guid AreaKPIs                    = new("10000007-0000-0000-0000-000000000000");
    public static readonly Guid AreaReports                 = new("10000008-0000-0000-0000-000000000000");
    public static readonly Guid AreaOrgChart                = new("10000009-0000-0000-0000-000000000000");
    public static readonly Guid AreaUserManagement          = new("10000010-0000-0000-0000-000000000000");
    public static readonly Guid AreaPermissionsManagement   = new("10000011-0000-0000-0000-000000000000");
    public static readonly Guid AreaRoles                   = new("10000012-0000-0000-0000-000000000000");

    // ── Action IDs (stable across re-runs) ───────────────────────────────────

    public static readonly Guid ActionView    = new("20000001-0000-0000-0000-000000000000");
    public static readonly Guid ActionCreate  = new("20000002-0000-0000-0000-000000000000");
    public static readonly Guid ActionEdit    = new("20000003-0000-0000-0000-000000000000");
    public static readonly Guid ActionDelete  = new("20000004-0000-0000-0000-000000000000");
    public static readonly Guid ActionApprove = new("20000005-0000-0000-0000-000000000000");
    public static readonly Guid ActionExport  = new("20000006-0000-0000-0000-000000000000");
    public static readonly Guid ActionAssign  = new("20000007-0000-0000-0000-000000000000");

    // ─────────────────────────────────────────────────────────────────────────

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var now = DateTime.UtcNow;
        int inserted = 0;

        inserted += await SeedAreasAsync(db, now, logger);
        inserted += await SeedActionsAsync(db, now, logger);
        inserted += await SeedMappingsAsync(db, now, logger);

        if (inserted > 0)
            logger.LogInformation("PermissionCatalogSeeder: inserted {Count} record(s).", inserted);
        else
            logger.LogInformation("PermissionCatalogSeeder: catalog already seeded, nothing to insert.");
    }

    // ── Areas ─────────────────────────────────────────────────────────────────

    private static async Task<int> SeedAreasAsync(AppDbContext db, DateTime now, ILogger logger)
    {
        var existingIds = await db.PermissionAreas
            .IgnoreQueryFilters()
            .Select(a => a.Id)
            .ToHashSetAsync();

        var areas = new[]
        {
            Area(AreaDashboard,             "Dashboard",             "Dashboard",             1),
            Area(AreaWorkspaces,            "Workspaces",            "Workspaces",            2),
            Area(AreaProjects,              "Projects",              "Projects",              3),
            Area(AreaMilestones,            "Milestones",            "Milestones",            4),
            Area(AreaActionItems,           "ActionItems",           "Action Items",          5),
            Area(AreaStrategicObjectives,   "StrategicObjectives",   "Strategic Objectives",  6),
            Area(AreaKPIs,                  "KPIs",                  "KPIs",                  7),
            Area(AreaReports,               "Reports",               "Reports",               8),
            Area(AreaOrgChart,              "OrgChart",              "Org Chart",             9),
            Area(AreaUserManagement,        "UserManagement",        "User Management",       10),
            Area(AreaPermissionsManagement, "PermissionsManagement", "Permissions Management",11),
            Area(AreaRoles,                 "Roles",                 "Roles Management",      12),
        };

        var toInsert = areas.Where(a => !existingIds.Contains(a.Id)).ToList();
        if (toInsert.Count == 0) return 0;

        foreach (var a in toInsert) { a.CreatedAt = now; a.CreatedBy = "system"; }
        await db.PermissionAreas.AddRangeAsync(toInsert);
        await db.SaveChangesAsync();
        return toInsert.Count;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private static async Task<int> SeedActionsAsync(AppDbContext db, DateTime now, ILogger logger)
    {
        var existingIds = await db.PermissionActions
            .IgnoreQueryFilters()
            .Select(a => a.Id)
            .ToHashSetAsync();

        var actions = new[]
        {
            Action(ActionView,    "View",    "View",    1),
            Action(ActionCreate,  "Create",  "Create",  2),
            Action(ActionEdit,    "Edit",    "Edit",    3),
            Action(ActionDelete,  "Delete",  "Delete",  4),
            Action(ActionApprove, "Approve", "Approve", 5),
            Action(ActionExport,  "Export",  "Export",  6),
            Action(ActionAssign,  "Assign",  "Assign",  7),
        };

        var toInsert = actions.Where(a => !existingIds.Contains(a.Id)).ToList();
        if (toInsert.Count == 0) return 0;

        foreach (var a in toInsert) { a.CreatedAt = now; a.CreatedBy = "system"; }
        await db.PermissionActions.AddRangeAsync(toInsert);
        await db.SaveChangesAsync();
        return toInsert.Count;
    }

    // ── Mappings ──────────────────────────────────────────────────────────────

    private static async Task<int> SeedMappingsAsync(AppDbContext db, DateTime now, ILogger logger)
    {
        var existingIds = await db.AreaPermissionMappings
            .IgnoreQueryFilters()
            .Select(m => m.Id)
            .ToHashSetAsync();

        // Deterministic mapping IDs: first 8 hex digits encode area index + action index
        // Format: 3000_AAII_0000_0000_0000_000000000000 where AA=area(01-12), II=action(01-07)
        static Guid MappingId(int areaIdx, int actionIdx)
            => new($"3000{areaIdx:D2}{actionIdx:D2}-0000-0000-0000-000000000000");

        // (areaId, areaName, areaIndex) × actions[]
        var desiredMappings = new List<(Guid AreaId, string AreaName, int AreaIdx, Guid ActionId, string ActionName, int ActionIdx)>
        {
            // Dashboard: View(1), Export(6)
            (AreaDashboard, "Dashboard", 1, ActionView,    "View",   1),
            (AreaDashboard, "Dashboard", 1, ActionExport,  "Export", 6),

            // Workspaces: View, Create, Edit, Delete
            (AreaWorkspaces, "Workspaces", 2, ActionView,   "View",   1),
            (AreaWorkspaces, "Workspaces", 2, ActionCreate, "Create", 2),
            (AreaWorkspaces, "Workspaces", 2, ActionEdit,   "Edit",   3),
            (AreaWorkspaces, "Workspaces", 2, ActionDelete, "Delete", 4),

            // Projects: View, Create, Edit, Delete, Approve, Export, Assign
            (AreaProjects, "Projects", 3, ActionView,    "View",    1),
            (AreaProjects, "Projects", 3, ActionCreate,  "Create",  2),
            (AreaProjects, "Projects", 3, ActionEdit,    "Edit",    3),
            (AreaProjects, "Projects", 3, ActionDelete,  "Delete",  4),
            (AreaProjects, "Projects", 3, ActionApprove, "Approve", 5),
            (AreaProjects, "Projects", 3, ActionExport,  "Export",  6),
            (AreaProjects, "Projects", 3, ActionAssign,  "Assign",  7),

            // Milestones: View, Create, Edit, Delete, Assign
            (AreaMilestones, "Milestones", 4, ActionView,   "View",   1),
            (AreaMilestones, "Milestones", 4, ActionCreate, "Create", 2),
            (AreaMilestones, "Milestones", 4, ActionEdit,   "Edit",   3),
            (AreaMilestones, "Milestones", 4, ActionDelete, "Delete", 4),
            (AreaMilestones, "Milestones", 4, ActionAssign, "Assign", 7),

            // ActionItems: View, Create, Edit, Delete, Approve, Assign, Export
            (AreaActionItems, "ActionItems", 5, ActionView,    "View",    1),
            (AreaActionItems, "ActionItems", 5, ActionCreate,  "Create",  2),
            (AreaActionItems, "ActionItems", 5, ActionEdit,    "Edit",    3),
            (AreaActionItems, "ActionItems", 5, ActionDelete,  "Delete",  4),
            (AreaActionItems, "ActionItems", 5, ActionApprove, "Approve", 5),
            (AreaActionItems, "ActionItems", 5, ActionExport,  "Export",  6),
            (AreaActionItems, "ActionItems", 5, ActionAssign,  "Assign",  7),

            // StrategicObjectives: View, Create, Edit, Delete
            (AreaStrategicObjectives, "StrategicObjectives", 6, ActionView,   "View",   1),
            (AreaStrategicObjectives, "StrategicObjectives", 6, ActionCreate, "Create", 2),
            (AreaStrategicObjectives, "StrategicObjectives", 6, ActionEdit,   "Edit",   3),
            (AreaStrategicObjectives, "StrategicObjectives", 6, ActionDelete, "Delete", 4),

            // KPIs: View, Create, Edit, Delete, Export
            (AreaKPIs, "KPIs", 7, ActionView,   "View",   1),
            (AreaKPIs, "KPIs", 7, ActionCreate, "Create", 2),
            (AreaKPIs, "KPIs", 7, ActionEdit,   "Edit",   3),
            (AreaKPIs, "KPIs", 7, ActionDelete, "Delete", 4),
            (AreaKPIs, "KPIs", 7, ActionExport, "Export", 6),

            // Reports: View, Create, Edit, Delete, Export
            (AreaReports, "Reports", 8, ActionView,   "View",   1),
            (AreaReports, "Reports", 8, ActionCreate, "Create", 2),
            (AreaReports, "Reports", 8, ActionEdit,   "Edit",   3),
            (AreaReports, "Reports", 8, ActionDelete, "Delete", 4),
            (AreaReports, "Reports", 8, ActionExport, "Export", 6),

            // OrgChart: View, Create, Edit, Delete
            (AreaOrgChart, "OrgChart", 9, ActionView,   "View",   1),
            (AreaOrgChart, "OrgChart", 9, ActionCreate, "Create", 2),
            (AreaOrgChart, "OrgChart", 9, ActionEdit,   "Edit",   3),
            (AreaOrgChart, "OrgChart", 9, ActionDelete, "Delete", 4),

            // UserManagement: View, Create, Edit, Delete, Assign
            (AreaUserManagement, "UserManagement", 10, ActionView,   "View",   1),
            (AreaUserManagement, "UserManagement", 10, ActionCreate, "Create", 2),
            (AreaUserManagement, "UserManagement", 10, ActionEdit,   "Edit",   3),
            (AreaUserManagement, "UserManagement", 10, ActionDelete, "Delete", 4),
            (AreaUserManagement, "UserManagement", 10, ActionAssign, "Assign", 7),

            // PermissionsManagement: View, Create, Edit, Delete
            (AreaPermissionsManagement, "PermissionsManagement", 11, ActionView,   "View",   1),
            (AreaPermissionsManagement, "PermissionsManagement", 11, ActionCreate, "Create", 2),
            (AreaPermissionsManagement, "PermissionsManagement", 11, ActionEdit,   "Edit",   3),
            (AreaPermissionsManagement, "PermissionsManagement", 11, ActionDelete, "Delete", 4),

            // Roles: View, Create, Edit, Delete, Assign
            (AreaRoles, "Roles", 12, ActionView,   "View",   1),
            (AreaRoles, "Roles", 12, ActionCreate, "Create", 2),
            (AreaRoles, "Roles", 12, ActionEdit,   "Edit",   3),
            (AreaRoles, "Roles", 12, ActionDelete, "Delete", 4),
            (AreaRoles, "Roles", 12, ActionAssign, "Assign", 7),
        };

        var toInsert = desiredMappings
            .Where(m => !existingIds.Contains(MappingId(m.Item3, m.Item6)))
            .Select(m => new AreaPermissionMapping
            {
                Id         = MappingId(m.Item3, m.Item6),
                AreaId     = m.AreaId,
                AreaName   = m.AreaName,
                ActionId   = m.ActionId,
                ActionName = m.ActionName,
                IsActive   = true,
                IsDeleted  = false,
                CreatedAt  = now,
                CreatedBy  = "system",
            })
            .ToList();

        if (toInsert.Count == 0) return 0;

        await db.AreaPermissionMappings.AddRangeAsync(toInsert);
        await db.SaveChangesAsync();
        return toInsert.Count;
    }

    // ── Entity factories ──────────────────────────────────────────────────────

    private static AppPermissionArea Area(Guid id, string name, string displayName, int order) =>
        new() { Id = id, Name = name, DisplayName = displayName, DisplayOrder = order, IsActive = true, IsDeleted = false };

    private static AppPermissionAction Action(Guid id, string name, string displayName, int order) =>
        new() { Id = id, Name = name, DisplayName = displayName, DisplayOrder = order, IsActive = true, IsDeleted = false };
}
