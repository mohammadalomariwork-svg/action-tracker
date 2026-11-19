namespace ActionTracker.Infrastructure.DataSync;

/// <summary>
/// Helpers for safely turning configured "schema.table" strings into
/// bracket-quoted SQL identifiers. Configuration values are trusted but we
/// still refuse anything that could break out of an identifier.
/// </summary>
public static class SqlIdentifier
{
    /// <summary>Splits "schema.table" (or "table") into (schema, table). Default schema is dbo.</summary>
    public static (string Schema, string Table) SplitTableName(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
            throw new ArgumentException("Table name is not configured.");

        var trimmed = configured.Trim().Replace("[", "").Replace("]", "");
        var parts   = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            1 => ("dbo", Validate(parts[0])),
            2 => (Validate(parts[0]), Validate(parts[1])),
            _ => throw new ArgumentException($"Table name '{configured}' must be in 'schema.table' form."),
        };
    }

    /// <summary>Returns "[schema].[table]".</summary>
    public static string Quote((string Schema, string Table) name)
        => $"{Quote(name.Schema)}.{Quote(name.Table)}";

    /// <summary>Bracket-quotes a single identifier, escaping any closing bracket.</summary>
    public static string Quote(string identifier)
        => "[" + Validate(identifier).Replace("]", "]]") + "]";

    private static string Validate(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 128)
            throw new ArgumentException($"Invalid SQL identifier '{identifier}'.");
        if (identifier.Any(ch => ch == ';' || ch == '\'' || ch == '-' && identifier.Contains("--") || char.IsControl(ch)))
            throw new ArgumentException($"Invalid SQL identifier '{identifier}'.");
        return identifier;
    }
}
