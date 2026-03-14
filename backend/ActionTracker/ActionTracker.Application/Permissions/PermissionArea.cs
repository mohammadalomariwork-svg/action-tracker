using System.ComponentModel;

namespace ActionTracker.Application.Permissions;

public enum PermissionArea
{
    [Description("Dashboard")]
    Dashboard = 1,

    [Description("Workspaces")]
    Workspaces = 2,

    [Description("Projects")]
    Projects = 3,

    [Description("Milestones")]
    Milestones = 4,

    [Description("Action Items")]
    ActionItems = 5,

    [Description("Strategic Objectives")]
    StrategicObjectives = 6,

    [Description("KPIs")]
    KPIs = 7,

    [Description("Reports")]
    Reports = 8,

    [Description("Org Chart")]
    OrgChart = 9,

    [Description("User Management")]
    UserManagement = 10,

    [Description("Permissions Management")]
    PermissionsManagement = 11,
}
