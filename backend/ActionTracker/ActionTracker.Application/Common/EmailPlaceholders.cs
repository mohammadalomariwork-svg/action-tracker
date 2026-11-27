using System.Net;

namespace ActionTracker.Application.Common;

/// <summary>
/// E-mail placeholder values. EmailSender puts placeholder values into the HTML
/// body as they are and HTML-decodes them only for the plain-text subject, so a
/// value taken from user input must be HTML-encoded first (WO-2026-11 A-3 rule).
/// </summary>
public static class EmailPlaceholders
{
    /// <summary>A copy of <paramref name="values"/> with every value HTML-encoded (null becomes empty).</summary>
    public static Dictionary<string, string> Encode(IReadOnlyDictionary<string, string> values) =>
        values.ToDictionary(kv => kv.Key, kv => WebUtility.HtmlEncode(kv.Value ?? string.Empty));
}
