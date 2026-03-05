using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Common;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ActionTracker.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ActionItem>          ActionItems          => Set<ActionItem>();
    public DbSet<RefreshToken>        RefreshTokens        => Set<RefreshToken>();
    public DbSet<KuEmployeeInfo>      KuEmployeeInfo       => Set<KuEmployeeInfo>();
    public DbSet<OrgUnit>             OrgUnits             => Set<OrgUnit>();
    public DbSet<StrategicObjective>  StrategicObjectives  => Set<StrategicObjective>();
    public DbSet<Kpi>                 Kpis                 => Set<Kpi>();
    public DbSet<KpiTarget>           KpiTargets           => Set<KpiTarget>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<ActionItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrgUnit>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<StrategicObjective>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<Kpi>().HasQueryFilter(o => !o.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
