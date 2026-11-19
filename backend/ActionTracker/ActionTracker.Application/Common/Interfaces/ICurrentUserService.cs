namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Exposes the authenticated user of the current request.
/// <see cref="UserId"/> is <c>null</c> outside an HTTP request (e.g. Hangfire
/// jobs, startup seeders) — callers treat that as "system context".
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
}
