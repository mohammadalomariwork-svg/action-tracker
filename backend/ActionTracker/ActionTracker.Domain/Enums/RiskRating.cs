using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum RiskRating
{
    [Description("Critical")]
    Critical = 0,

    [Description("High")]
    High = 1,

    [Description("Medium")]
    Medium = 2,

    [Description("Low")]
    Low = 3
}
