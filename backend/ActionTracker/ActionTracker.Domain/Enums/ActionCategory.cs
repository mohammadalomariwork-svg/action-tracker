using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ActionCategory
{
    [Description("Operations")]
    Operations = 1,

    [Description("Strategic")]
    Strategic = 2,

    [Description("HR")]
    HR = 3,

    [Description("Finance")]
    Finance = 4,

    [Description("IT")]
    IT = 5,

    [Description("Compliance")]
    Compliance = 6,

    [Description("Communication")]
    Communication = 7
}
