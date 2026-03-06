using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.Workspaces.Models;
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
    public DbSet<Workspace>           Workspaces           => Set<Workspace>();
    public DbSet<WorkspaceAdmin>      WorkspaceAdmins      => Set<WorkspaceAdmin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<ActionItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrgUnit>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<StrategicObjective>().HasQueryFilter(o => !o.IsDeleted);
        modelBuilder.Entity<Kpi>().HasQueryFilter(o => !o.IsDeleted);

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.OrganizationUnit)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.CreatedAt)
                  .IsRequired()
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .IsRequired(false);

            entity.Property(e => e.IsActive)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.HasIndex(e => e.OrganizationUnit);

            entity.HasMany(e => e.Admins)
                  .WithOne(a => a.Workspace)
                  .HasForeignKey(a => a.WorkspaceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkspaceAdmin>(entity =>
        {
            entity.ToTable("WorkspaceAdmins");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .UseIdentityColumn();

            entity.Property(e => e.AdminUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.AdminUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => e.AdminUserId);
        });
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
