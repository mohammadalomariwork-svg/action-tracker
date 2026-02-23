using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ActionTracker.Tests.Integration;

/// <summary>
/// Shared WebApplicationFactory that replaces the SQL Server database with an
/// isolated InMemory database. Each factory instance gets its own unique database
/// name so test classes do not share state.
/// </summary>
public class ActionTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server DbContextOptions registered by Program.cs
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (optionsDescriptor is not null)
                services.Remove(optionsDescriptor);

            // Remove the existing AppDbContext registration to avoid duplicates
            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));

            if (contextDescriptor is not null)
                services.Remove(contextDescriptor);

            // Register a fresh InMemory database scoped to this factory instance
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
