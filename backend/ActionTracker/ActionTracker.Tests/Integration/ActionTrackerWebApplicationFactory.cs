using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// Shared WebApplicationFactory that replaces the SQL Server database with an
/// isolated InMemory database. Each factory instance gets its own unique database
/// name so test classes do not share state.
/// </summary>
public class ActionTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly object HostStartLock = new();

    private readonly string _dbName = Guid.NewGuid().ToString();

    /// <summary>
    /// Program.cs wires Serilog through the static <c>Log.Logger</c> bootstrap
    /// logger, which can be frozen only once. Test classes run in parallel, so
    /// two hosts starting at the same moment trip over each other ("The entry
    /// point exited without ever building an IHost"). Start them one at a time.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (HostStartLock)
        {
            return base.CreateHost(builder);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove everything Program.cs registered for the SQL Server
            // context. Since EF Core 9, AddDbContext also registers an
            // IDbContextOptionsConfiguration<T> carrying the UseSqlServer call;
            // leaving it behind registers two providers and the host fails.
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext))
                .ToList();

            foreach (var descriptor in toRemove)
                services.Remove(descriptor);

            // Register a fresh InMemory database scoped to this factory instance
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>
    /// Creates a local-login user holding <paramref name="role"/> (idempotent).
    /// Roles and their default permissions are seeded by the app at startup.
    /// </summary>
    public async Task EnsureUserAsync(string email, string password, string role)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            FirstName      = "Integration",
            LastName       = role,
            LoginProvider  = "Local",
            IsActive       = true,
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = "integration-test",
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                "Could not create test user: " + string.Join("; ", created.Errors.Select(e => e.Description)));

        var granted = await userManager.AddToRoleAsync(user, role);
        if (!granted.Succeeded)
            throw new InvalidOperationException(
                "Could not assign test role: " + string.Join("; ", granted.Errors.Select(e => e.Description)));
    }
}
