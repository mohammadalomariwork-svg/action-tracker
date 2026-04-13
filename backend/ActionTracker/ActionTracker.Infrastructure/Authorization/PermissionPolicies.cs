namespace ActionTracker.Infrastructure.Authorization;

/// <summary>
/// Named policy constants for every protected area/action combination.
/// Each constant's value is the string used in <c>[Authorize(Policy = ...)]</c>.
/// Format: "Area.Action".
/// </summary>
public static class PermissionPolicies
{
    // ── Dashboard ─────────────────────────────────────────────────────────────
    public const string DashboardView = "Dashboard.View";

    // ── Workspaces ────────────────────────────────────────────────────────────
    public const string WorkspacesView   = "Workspaces.View";
    public const string WorkspacesCreate = "Workspaces.Create";
    public const string WorkspacesEdit   = "Workspaces.Edit";
    public const string WorkspacesDelete = "Workspaces.Delete";

    // ── Projects ──────────────────────────────────────────────────────────────
    public const string ProjectsView    = "Projects.View";
    public const string ProjectsCreate  = "Projects.Create";
    public const string ProjectsEdit    = "Projects.Edit";
    public const string ProjectsDelete  = "Projects.Delete";
    public const string ProjectsApprove = "Projects.Approve";
    public const string ProjectsExport  = "Projects.Export";

    // ── Milestones ────────────────────────────────────────────────────────────
    public const string MilestonesView   = "Milestones.View";
    public const string MilestonesCreate = "Milestones.Create";
    public const string MilestonesEdit   = "Milestones.Edit";
    public const string MilestonesDelete = "Milestones.Delete";
    public const string MilestonesAssign = "Milestones.Assign";

    // ── Action Items ──────────────────────────────────────────────────────────
    public const string ActionItemsView   = "ActionItems.View";
    public const string ActionItemsCreate = "ActionItems.Create";
    public const string ActionItemsEdit   = "ActionItems.Edit";
    public const string ActionItemsDelete = "ActionItems.Delete";
    public const string ActionItemsAssign  = "ActionItems.Assign";
    public const string ActionItemsApprove = "ActionItems.Approve";

    // ── Strategic Objectives ──────────────────────────────────────────────────
    public const string StrategicObjectivesView   = "StrategicObjectives.View";
    public const string StrategicObjectivesCreate = "StrategicObjectives.Create";
    public const string StrategicObjectivesEdit   = "StrategicObjectives.Edit";
    public const string StrategicObjectivesDelete = "StrategicObjectives.Delete";

    // ── KPIs ──────────────────────────────────────────────────────────────────
    public const string KPIsView   = "KPIs.View";
    public const string KPIsCreate = "KPIs.Create";
    public const string KPIsEdit   = "KPIs.Edit";
    public const string KPIsDelete = "KPIs.Delete";

    // ── Reports ───────────────────────────────────────────────────────────────
    public const string ReportsView   = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string ReportsCreate = "Reports.Create";
    public const string ReportsEdit   = "Reports.Edit";
    public const string ReportsDelete = "Reports.Delete";

    // ── OrgChart ──────────────────────────────────────────────────────────────
    public const string OrgChartView   = "OrgChart.View";
    public const string OrgChartCreate = "OrgChart.Create";
    public const string OrgChartEdit   = "OrgChart.Edit";
    public const string OrgChartDelete = "OrgChart.Delete";

    // ── User Management ───────────────────────────────────────────────────────
    public const string UserManagementView   = "UserManagement.View";
    public const string UserManagementCreate = "UserManagement.Create";
    public const string UserManagementEdit   = "UserManagement.Edit";
    public const string UserManagementDelete = "UserManagement.Delete";

    // ── Permissions Management ────────────────────────────────────────────────
    public const string PermissionsManagementView    = "PermissionsManagement.View";
    public const string PermissionsManagementCreate  = "PermissionsManagement.Create";
    public const string PermissionsManagementEdit    = "PermissionsManagement.Edit";
    public const string PermissionsManagementDelete  = "PermissionsManagement.Delete";
    public const string PermissionsManagementApprove = "PermissionsManagement.Approve";

    // ── Roles ─────────────────────────────────────────────────────────────────
    public const string RolesView   = "Roles.View";
    public const string RolesCreate = "Roles.Create";
    public const string RolesEdit   = "Roles.Edit";
    public const string RolesDelete = "Roles.Delete";
    public const string RolesAssign = "Roles.Assign";

    // ── Risks ─────────────────────────────────────────────────────────────────
    public const string RisksView   = "Risks.View";
    public const string RisksCreate = "Risks.Create";
    public const string RisksEdit   = "Risks.Edit";
    public const string RisksDelete = "Risks.Delete";
    public const string RisksExport = "Risks.Export";

    // ── Email Templates ───────────────────────────────────────────────────────
    public const string EmailTemplatesView = "EmailTemplates.View";
    public const string EmailTemplatesEdit = "EmailTemplates.Edit";

    // ── Notifications ─────────────────────────────────────────────────────────
    public const string NotificationsView   = "Notifications.View";
    public const string NotificationsDelete = "Notifications.Delete";
}
