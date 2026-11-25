namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Role membership from the database (ASP.NET Identity), the same source as
/// the other role checks in the Infrastructure services — independent of
/// whether the sign-in token carried role claims.
/// </summary>
public interface IUserRoleService
{
    Task<bool> IsInRoleAsync(string userId, string role, CancellationToken ct = default);
}
