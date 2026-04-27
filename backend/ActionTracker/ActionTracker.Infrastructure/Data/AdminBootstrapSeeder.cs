using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Ensures a bootstrap Admin user exists. Idempotent — if a user with the
/// target email already exists, the seeder only makes sure they hold the Admin
/// role and does not touch the password.
/// </summary>
public static class AdminBootstrapSeeder
{
    private const string AdminEmail     = "mohammad.khalifa.omari@gmail.com";
    private const string AdminPassword  = "Test@4321";
    private const string AdminFirstName = "Mohammad";
    private const string AdminLastName  = "Al-Omari";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope  = services.CreateScope();
        var userManager  = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger       = scope.ServiceProvider
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger(nameof(AdminBootstrapSeeder));

        var existing = await userManager.FindByEmailAsync(AdminEmail);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, AppRoles.Admin))
            {
                var addResult = await userManager.AddToRoleAsync(existing, AppRoles.Admin);
                if (addResult.Succeeded)
                    logger.LogInformation("Granted Admin role to existing user {Email}", AdminEmail);
                else
                    logger.LogWarning("Failed to grant Admin role to {Email}: {Errors}",
                        AdminEmail, string.Join("; ", addResult.Errors.Select(e => e.Description)));
            }
            return;
        }

        var user = new ApplicationUser
        {
            UserName       = AdminEmail,
            Email          = AdminEmail,
            FirstName      = AdminFirstName,
            LastName       = AdminLastName,
            LoginProvider  = "Local",
            IsActive       = true,
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = "AdminBootstrapSeeder",
        };

        var createResult = await userManager.CreateAsync(user, AdminPassword);
        if (!createResult.Succeeded)
        {
            logger.LogError("Failed to create bootstrap admin {Email}: {Errors}",
                AdminEmail, string.Join("; ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to assign Admin role to {Email}: {Errors}",
                AdminEmail, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Bootstrap admin {Email} created and assigned Admin role", AdminEmail);
    }
}
