using System.ComponentModel;

namespace ActionTracker.Domain.Enums;

public enum ProjectType
{
    [Description("Operational")]
    Operational = 1,

    [Description("Strategic")]
    Strategic = 2
}
