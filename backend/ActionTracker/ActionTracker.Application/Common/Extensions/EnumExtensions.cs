using System.ComponentModel;
using System.Reflection;

namespace ActionTracker.Application.Common.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Returns the [Description] attribute value for an enum member,
    /// falling back to the member name if no attribute is present.
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attr  = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString();
    }
}
