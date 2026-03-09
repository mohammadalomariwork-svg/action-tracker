using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ProjectStatus
{
    [Description("Draft")]
    Draft = 1,

    [Description("Active")]
    Active = 2,

    [Description("On Hold")]
    OnHold = 3,

    [Description("Completed")]
    Completed = 4,

    [Description("Cancelled")]
    Cancelled = 5
}
