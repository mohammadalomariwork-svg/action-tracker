using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;

namespace ActionTracker.Tests.Unit;

/// <summary>
/// Puts a user into a system role in the (InMemory) database. WO-2026-11 P-2b reads
/// the request user's roles from AspNetUserRoles (<c>IAppDbContext.CurrentUserRoles</c>),
/// like the permission policies, so a test Admin must be an Admin there.
/// </summary>
public static class TestRoles
{
    public static void Grant(AppDbContext db, string userId, string roleName)
    {
        var role = db.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                   ?? db.Roles.FirstOrDefault(r => r.Name == roleName);
        if (role is null)
        {
            role = new IdentityRole(roleName) { NormalizedName = roleName.ToUpperInvariant() };
            db.Roles.Add(role);
        }
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });
    }
}
