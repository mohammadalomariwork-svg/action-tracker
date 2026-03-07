using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ActionPriority
{
    [Description("Low")]
    Low = 1,

    [Description("Medium")]
    Medium = 2,

    [Description("High")]
    High = 3,

    [Description("Critical")]
    Critical = 4
}
