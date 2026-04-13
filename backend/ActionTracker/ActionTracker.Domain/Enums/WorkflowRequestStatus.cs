using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum WorkflowRequestStatus
{
    [Description("Pending")]
    Pending = 0,

    [Description("Approved")]
    Approved = 1,

    [Description("Rejected")]
    Rejected = 2
}
