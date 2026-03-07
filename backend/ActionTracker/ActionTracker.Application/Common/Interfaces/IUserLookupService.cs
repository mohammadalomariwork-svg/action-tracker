namespace ActionTracker.Application.Common.Interfaces;

/// <summary>Resolves user IDs (from AspNetUsers) to display names (FirstName LastName).</summary>
public interface IUserLookupService
{
    /// <summary>Returns "FirstName LastName" for the given user ID, or null if not found.</summary>
    Task<string?> GetDisplayNameAsync(string? userId, CancellationToken ct = default);

    /// <summary>Returns a dictionary of userId → "FirstName LastName" for all given IDs.</summary>
    Task<Dictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default);
}
