using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ActionTracker.Infrastructure.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations add</c> can build the
/// <see cref="AppDbContext"/> from the Infrastructure project alone — without
/// needing to compile / load the API startup project. Used only by EF tooling;
/// at runtime the API's DI container supplies the DbContext.
/// </summary>
public class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ActionTrackerDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;
        return new AppDbContext(options);
    }
}
