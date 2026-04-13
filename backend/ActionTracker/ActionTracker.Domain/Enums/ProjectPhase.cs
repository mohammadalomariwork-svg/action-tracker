using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ProjectPhase
{
    [Description("Initiation")]
    Initiation = 1,

    [Description("Planning")]
    Planning = 2,

    [Description("Execution")]
    Execution = 3,

    [Description("Monitoring & Controlling")]
    MonitoringAndControlling = 4,

    [Description("Closing")]
    Closing = 5
}
