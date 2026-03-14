using System.ComponentModel;

namespace ActionTracker.Application.Permissions;

public enum PermissionAction
{
    [Description("View")]
    View = 1,

    [Description("Create")]
    Create = 2,

    [Description("Edit")]
    Edit = 3,

    [Description("Delete")]
    Delete = 4,

    [Description("Approve")]
    Approve = 5,

    [Description("Export")]
    Export = 6,

    [Description("Assign")]
    Assign = 7,
}
