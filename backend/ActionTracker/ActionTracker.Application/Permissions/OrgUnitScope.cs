using System.ComponentModel;

namespace ActionTracker.Application.Permissions;

public enum OrgUnitScope
{
    [Description("All")]
    All = 1,

    [Description("Specific Org Unit")]
    SpecificOrgUnit = 2,

    [Description("Own Only")]
    OwnOnly = 3,
}
