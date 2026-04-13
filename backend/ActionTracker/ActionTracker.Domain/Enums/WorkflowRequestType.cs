using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum WorkflowRequestType
{
    [Description("Date Change Request")]
    DateChangeRequest = 0,

    [Description("Status Change Request")]
    StatusChangeRequest = 1
}
