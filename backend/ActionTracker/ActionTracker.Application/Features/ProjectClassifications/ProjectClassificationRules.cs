using System.Text.RegularExpressions;

namespace ActionTracker.Application.Features.ProjectClassifications;

/// <summary>
/// Name rules for internal project classifications (WO-2026-11 P-1). Pure, so
/// the validators, the service and the tests share one copy. Client copy:
/// <c>shared/utils/project-classification-rules.util.ts</c> — change both together.
/// <list type="bullet">
/// <item>Leading / trailing spaces are removed and runs of white space inside become one space.</item>
/// <item>1 to <see cref="MaxNameLength"/> characters after that; no control characters.</item>
/// <item>Unique within the workspace without regard to case (<see cref="Normalize"/>), among live classifications.</item>
/// </list>
/// </summary>
public static partial class ProjectClassificationRules
{
    public const int MaxNameLength = 100;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRun();

    /// <summary>Trimmed, with every run of white space turned into one space.</summary>
    public static string Clean(string? raw) =>
        raw is null ? string.Empty : WhiteSpaceRun().Replace(raw.Trim(), " ");

    /// <summary>The key names are compared by: upper-case invariant of the cleaned name.</summary>
    public static string Normalize(string cleanedName) => cleanedName.ToUpperInvariant();

    /// <summary>The rule <paramref name="raw"/> breaks, in the words the API returns, or null.</summary>
    public static string? NameProblem(string? raw)
    {
        var name = Clean(raw);
        if (name.Length == 0)
            return "Enter a name for the classification.";
        if (name.Any(char.IsControl))
            return "The classification name contains a character that is not allowed.";
        if (name.Length > MaxNameLength)
            return $"The classification name can be at most {MaxNameLength} characters.";
        return null;
    }

    /// <summary>The cleaned name; throws <see cref="ArgumentException"/> (400) when a rule is broken.</summary>
    public static string CleanOrThrow(string? raw)
    {
        var problem = NameProblem(raw);
        if (problem is not null)
            throw new ArgumentException(problem);
        return Clean(raw);
    }

    /// <summary>The message for a name the workspace already has (409).</summary>
    public static string DuplicateName(string name) =>
        $"This workspace already has a classification named \"{name}\" (names are compared without regard to case).";
}

/// <summary>
/// A classification change the current state forbids (409): a duplicate name,
/// or deleting one that projects still use. The message says why.
/// </summary>
public sealed class ProjectClassificationConflictException : Exception
{
    public ProjectClassificationConflictException(string message) : base(message) { }
}
