using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum RiskStatus
{
    [Description("Open")]
    Open = 0,

    [Description("Mitigating")]
    Mitigating = 1,

    [Description("Accepted")]
    Accepted = 2,

    [Description("Transferred")]
    Transferred = 3,

    [Description("Closed")]
    Closed = 4
}
