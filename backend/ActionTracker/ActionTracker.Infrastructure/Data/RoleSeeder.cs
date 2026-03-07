using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Ensures the four application roles exist in the database at startup.
/// Safe to call multiple times — uses <see cref="RoleManager{TRole}.RoleExistsAsync"/>
/// before every create, so no duplicates are created.
/// </summary>
public static class RoleSeeder
{
    private static readonly string[] Roles = ["Admin", "Manager", "User", "Viewer"];

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope       = services.CreateScope();
        var roleManager       = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger            = scope.ServiceProvider
                                     .GetRequiredService<ILoggerFactory>()
                                     .CreateLogger(nameof(RoleSeeder));

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
