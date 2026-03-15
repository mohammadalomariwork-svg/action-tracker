using ActionTracker.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Ensures all application roles exist in the database at startup.
/// Safe to call multiple times — uses <see cref="RoleManager{TRole}.RoleExistsAsync"/>
/// before every create, so no duplicates are ever created.
/// </summary>
public static class RoleSeeder
{
    private static readonly string[] Roles =
    [
        // System-level
        AppRoles.Admin,
        AppRoles.WorkspaceAdmin,

        // PMO
        AppRoles.PmoHead,
        AppRoles.PmoAnalyst,

        // Project
        AppRoles.ProjectSponsor,
        AppRoles.ProjectManager,
        AppRoles.ProjectCoordinator,
        AppRoles.TeamMember,

        // Legacy read-only role
        AppRoles.Viewer,
    ];

    /// <summary>Roles that have been retired and should be deleted on startup.</summary>
    private static readonly string[] DeprecatedRoles = [AppRoles.Manager, AppRoles.User];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope       = services.CreateScope();
        var roleManager       = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger            = scope.ServiceProvider
                                     .GetRequiredService<ILoggerFactory>()
                                     .CreateLogger(nameof(RoleSeeder));

        // Delete deprecated roles if they still exist in the database.
        foreach (var deprecated in DeprecatedRoles)
        {
            var existing = await roleManager.FindByNameAsync(deprecated);
            if (existing is null) continue;

            var deleteResult = await roleManager.DeleteAsync(existing);
            if (deleteResult.Succeeded)
                logger.LogInformation("Deprecated role '{Role}' deleted.", deprecated);
            else
                logger.LogWarning("Failed to delete deprecated role '{Role}': {Errors}",
                    deprecated, string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
        }

        foreach (var role in Roles)
        {
            if (await roleManager.RoleExistsAsync(role)) continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (result.Succeeded)
                logger.LogInformation("Role '{Role}' seeded", role);
            else
                logger.LogWarning("Failed to seed role '{Role}': {Errors}",
                    role, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
