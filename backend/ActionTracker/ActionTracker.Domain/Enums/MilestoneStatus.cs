using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum MilestoneStatus
{
    [Description("Not Started")]
    NotStarted = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Delayed")]
    Delayed = 4,

    [Description("Cancelled")]
    Cancelled = 5
}
