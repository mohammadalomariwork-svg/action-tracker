using System.Text.RegularExpressions;

namespace ActionTracker.Application.Features.Tags;

/// <summary>
/// The tag rules (WO-2026-11 A-5, Q14 + D6). Pure, so the validators, the
/// service and the tests share one copy. Client copy:
/// <c>shared/utils/tag-rules.util.ts</c> — change both together.
/// <list type="bullet">
/// <item>Leading / trailing spaces are removed and runs of white space inside become one space.</item>
/// <item>Duplicates are dropped without regard to case (the first spelling wins).</item>
/// <item>At most <see cref="MaxTagsPerItem"/> tags, each at most <see cref="MaxNameLength"/> characters.</item>
/// <item>Empty tags, commas and control characters are refused.</item>
/// </list>
/// </summary>
public static partial class TagRules
{
    public const int MaxTagsPerItem = 10;
    public const int MaxNameLength  = 30;

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRun();

    /// <summary>Trimmed, with every run of white space turned into one space.</summary>
    public static string Clean(string? raw) =>
        raw is null ? string.Empty : WhiteSpaceRun().Replace(raw.Trim(), " ");

    /// <summary>The key a tag is matched by: upper-case invariant of the cleaned name.</summary>
    public static string Normalize(string cleanedName) => cleanedName.ToUpperInvariant();

    /// <summary>
    /// The first rule <paramref name="tags"/> breaks, in the words the API
    /// returns, or null when they are acceptable. Null input is acceptable
    /// (it means "leave the tags as they are").
    /// </summary>
    public static string? Problem(IEnumerable<string?>? tags)
    {
        if (tags is null) return null;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in tags)
        {
            var name = Clean(raw);
            if (name.Length == 0)
                return "A tag cannot be empty.";
            if (name.Contains(','))
                return $"Tag \"{name}\" cannot contain a comma.";
            if (name.Any(char.IsControl))
                return $"Tag \"{Printable(name)}\" contains a character that is not allowed.";
            if (name.Length > MaxNameLength)
                return $"Tag \"{name}\" is longer than {MaxNameLength} characters.";
            seen.Add(Normalize(name));
        }

        return seen.Count > MaxTagsPerItem
            ? $"An action item can have at most {MaxTagsPerItem} tags."
            : null;
    }

    /// <summary>
    /// The cleaned, de-duplicated names in the order given (first spelling of
    /// each wins). Throws <see cref="TagRuleException"/> when a rule is broken.
    /// </summary>
    public static IReadOnlyList<string> CleanAll(IEnumerable<string?> tags)
    {
        var list = tags.ToList();
        var problem = Problem(list);
        if (problem is not null)
            throw new TagRuleException(problem);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var raw in list)
        {
            var name = Clean(raw);
            if (seen.Add(Normalize(name)))
                result.Add(name);
        }
        return result;
    }

    private static string Printable(string name) =>
        new(name.Select(c => char.IsControl(c) ? '?' : c).ToArray());
}

/// <summary>A tag rule was broken (400 with the message).</summary>
public sealed class TagRuleException : ArgumentException
{
    public TagRuleException(string message) : base(message) { }
}

/// <summary>
/// The tag tables are not in the database yet (migration pending). 503 with
/// the message; reads and saves without tags are not affected.
/// </summary>
public sealed class TagsUnavailableException : Exception
{
    public const string DefaultMessage =
        "Tags are not available until the database update for tags (Add_ActionItemTags) has been applied.";

    public TagsUnavailableException() : base(DefaultMessage) { }
}
