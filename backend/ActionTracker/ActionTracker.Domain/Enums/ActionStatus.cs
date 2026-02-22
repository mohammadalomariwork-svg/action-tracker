using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ActionStatus
{
    [Description("To Do")]
    ToDo = 1,

    [Description("In Progress")]
    InProgress = 2,

    [Description("In Review")]
    InReview = 3,

    [Description("Done")]
    Done = 4,

    [Description("Overdue")]
    Overdue = 5
}
