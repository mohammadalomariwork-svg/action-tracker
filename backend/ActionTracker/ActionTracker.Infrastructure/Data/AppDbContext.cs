using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Domain.Common;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

// Alias the Projects models namespace to disambiguate from identically-named
// domain entities (ActionItem, StrategicObjective) that pre-exist in the schema.
using PM = ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // ── Legacy / shared domain sets ───────────────────────────────────────────
    public DbSet<ActionItem>           ActionItems           => Set<ActionItem>();
    public DbSet<ActionItemAssignee>   ActionItemAssignees   => Set<ActionItemAssignee>();
    public DbSet<ActionItemEscalation> ActionItemEscalations => Set<ActionItemEscalation>();
    public DbSet<RefreshToken>        RefreshTokens        => Set<RefreshToken>();
    public DbSet<KuEmployeeInfo>      KuEmployeeInfo       => Set<KuEmployeeInfo>();
    public DbSet<OrgUnit>             OrgUnits             => Set<OrgUnit>();
    public DbSet<StrategicObjective>  StrategicObjectives  => Set<StrategicObjective>();
    public DbSet<Kpi>                 Kpis                 => Set<Kpi>();
    public DbSet<KpiTarget>           KpiTargets           => Set<KpiTarget>();
    public DbSet<Workspace>           Workspaces           => Set<Workspace>();
    public DbSet<WorkspaceAdmin>      WorkspaceAdmins      => Set<WorkspaceAdmin>();

    // ── Projects feature sets ─────────────────────────────────────────────────
    // NOTE: PM.StrategicObjective (workspace-scoped, int PK) is a different
    //       entity from the domain StrategicObjective (admin panel, Guid PK).
    //       It maps to "WorkspaceStrategicObjectives" to avoid a table clash.
    public DbSet<PM.StrategicObjective>      WorkspaceStrategicObjectives => Set<PM.StrategicObjective>();
    public DbSet<PM.Project>                 Projects                     => Set<PM.Project>();
    public DbSet<PM.Milestone>               Milestones                   => Set<PM.Milestone>();
    // NOTE: PM.ActionItem (workspace/project/milestone-scoped) is different from
    //       the legacy domain ActionItem. Maps to "ProjectActionItems".
    public DbSet<PM.ActionItem>              ProjectActionItems            => Set<PM.ActionItem>();
    public DbSet<PM.Comment>                 Comments                     => Set<PM.Comment>();
    public DbSet<PM.ProjectDocument>         ProjectDocuments              => Set<PM.ProjectDocument>();
    public DbSet<PM.ActionDocument>          ActionDocuments               => Set<PM.ActionDocument>();
    public DbSet<PM.ProjectBudget>           ProjectBudgets                => Set<PM.ProjectBudget>();
    public DbSet<PM.Contract>                Contracts                    => Set<PM.Contract>();
    public DbSet<PM.ProjectBaseline>         ProjectBaselines              => Set<PM.ProjectBaseline>();
    public DbSet<PM.BaselineChangeRequest>   BaselineChangeRequests        => Set<PM.BaselineChangeRequest>();

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
                  .HasColumnType("uniqueidentifier")
                  .HasDefaultValueSql("NEWID()")
                  .ValueGeneratedOnAdd();

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

        // ── WorkspaceStrategicObjective ───────────────────────────────────────
        // Workspace-scoped strategic objectives (int PK). Distinct from the
        // admin-panel StrategicObjective (Guid PK) which owns "StrategicObjectives".
        modelBuilder.Entity<PM.StrategicObjective>(entity =>
        {
            entity.ToTable("WorkspaceStrategicObjectives");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.OrganizationUnit)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.FiscalYear)
                  .IsRequired();

            entity.Property(e => e.IsActive)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.OrganizationUnit, e.IsActive });
        });

        // ── Project ───────────────────────────────────────────────────────────
        modelBuilder.Entity<PM.Project>(entity =>
        {
            entity.ToTable("Projects");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.Description)
                  .HasMaxLength(2000);

            entity.Property(e => e.SponsorUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.SponsorUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.ProjectManagerUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.ProjectManagerUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.CreatedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.BaselinedByUserId)
                  .HasMaxLength(450);

            entity.Property(e => e.Status)
                  .HasConversion<int>()
                  .HasDefaultValue(PM.ProjectStatus.Draft);

            entity.Property(e => e.ProjectType)
                  .HasConversion<int>();

            entity.Property(e => e.IsBaselined)
                  .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            // FK to Workspace — restrict so deleting a workspace is blocked
            // while it still has projects.
            entity.HasOne(e => e.Workspace)
                  .WithMany()
                  .HasForeignKey(e => e.WorkspaceId)
                  .OnDelete(DeleteBehavior.Restrict);

            // FK to WorkspaceStrategicObjective — nullable, set null on delete.
            entity.HasOne(e => e.StrategicObjective)
                  .WithMany(o => o.Projects)
                  .HasForeignKey(e => e.StrategicObjectiveId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.WorkspaceId, e.Status, e.IsActive });
        });

        // ── Milestone ─────────────────────────────────────────────────────────
        modelBuilder.Entity<PM.Milestone>(entity =>
        {
            entity.ToTable("Milestones");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.SequenceOrder)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasConversion<int>()
                  .HasDefaultValue(PM.MilestoneStatus.NotStarted);

            entity.Property(e => e.CompletionPercentage)
                  .HasDefaultValue(0);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Milestones)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ProjectId, e.SequenceOrder });
        });

        // ── ProjectActionItem ─────────────────────────────────────────────────
        // Maps to "ProjectActionItems" (not "ActionItems") to avoid clashing with
        // the legacy domain ActionItem that already owns that table.
        modelBuilder.Entity<PM.ActionItem>(entity =>
        {
            entity.ToTable("ProjectActionItems");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.Description)
                  .HasMaxLength(2000);

            entity.Property(e => e.Status)
                  .HasConversion<int>()
                  .HasDefaultValue(PM.ActionItemStatus.NotStarted);

            entity.Property(e => e.Priority)
                  .HasConversion<int>()
                  .HasDefaultValue(PM.ActionItemPriority.Medium);

            entity.Property(e => e.AssignedToUserId)
                  .HasMaxLength(450);

            entity.Property(e => e.AssignedToUserName)
                  .HasMaxLength(256);

            entity.Property(e => e.AssignedToExternalName)
                  .HasMaxLength(256);

            entity.Property(e => e.AssignedToExternalEmail)
                  .HasMaxLength(256);

            entity.Property(e => e.IsExternalAssignee)
                  .HasDefaultValue(false);

            entity.Property(e => e.CompletionPercentage)
                  .HasDefaultValue(0);

            entity.Property(e => e.CreatedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Workspace)
                  .WithMany()
                  .HasForeignKey(e => e.WorkspaceId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.ActionItems)
                  .HasForeignKey(e => e.ProjectId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Milestone)
                  .WithMany(m => m.ActionItems)
                  .HasForeignKey(e => e.MilestoneId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.WorkspaceId, e.ProjectId, e.MilestoneId, e.Status });
        });

        // ── Comment ───────────────────────────────────────────────────────────
        modelBuilder.Entity<PM.Comment>(entity =>
        {
            entity.ToTable("Comments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Content)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(e => e.AuthorUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.AuthorUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.IsEdited)
                  .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.ActionItem)
                  .WithMany(a => a.Comments)
                  .HasForeignKey(e => e.ActionItemId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Milestone)
                  .WithMany(m => m.Comments)
                  .HasForeignKey(e => e.MilestoneId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Comments)
                  .HasForeignKey(e => e.ProjectId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ProjectDocument ───────────────────────────────────────────────────
        modelBuilder.Entity<PM.ProjectDocument>(entity =>
        {
            entity.ToTable("ProjectDocuments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.FileName)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.StoredFileName)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.ContentType)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.UploadedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.UploadedByUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.UploadedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Documents)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ActionDocument ────────────────────────────────────────────────────
        modelBuilder.Entity<PM.ActionDocument>(entity =>
        {
            entity.ToTable("ActionDocuments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.Title)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(e => e.FileName)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.StoredFileName)
                  .IsRequired()
                  .HasMaxLength(500);

            entity.Property(e => e.ContentType)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.UploadedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.UploadedByUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.UploadedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.ActionItem)
                  .WithMany(a => a.Documents)
                  .HasForeignKey(e => e.ActionItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProjectBudget (1-to-1 with Project) ──────────────────────────────
        modelBuilder.Entity<PM.ProjectBudget>(entity =>
        {
            entity.ToTable("ProjectBudgets");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.TotalBudget)
                  .IsRequired()
                  .HasPrecision(18, 2);

            entity.Property(e => e.SpentAmount)
                  .HasPrecision(18, 2)
                  .HasDefaultValue(0m);

            entity.Property(e => e.Currency)
                  .IsRequired()
                  .HasMaxLength(10)
                  .HasDefaultValue("AED");

            entity.Property(e => e.BudgetNotes)
                  .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithOne(p => p.Budget)
                  .HasForeignKey<PM.ProjectBudget>(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Contract ──────────────────────────────────────────────────────────
        modelBuilder.Entity<PM.Contract>(entity =>
        {
            entity.ToTable("Contracts");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.ContractNumber)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(e => e.ContractorName)
                  .IsRequired()
                  .HasMaxLength(300);

            entity.Property(e => e.ContractorContact)
                  .HasMaxLength(300);

            entity.Property(e => e.ContractValue)
                  .HasPrecision(18, 2);

            entity.Property(e => e.Currency)
                  .HasMaxLength(10)
                  .HasDefaultValue("AED");

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.IsActive)
                  .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.Contracts)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProjectBaseline (1-to-1 with Project) ────────────────────────────
        modelBuilder.Entity<PM.ProjectBaseline>(entity =>
        {
            entity.ToTable("ProjectBaselines");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.BaselinedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.BaselinedByUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.BaselineSnapshotJson)
                  .IsRequired()
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.BaselinedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithOne(p => p.Baseline)
                  .HasForeignKey<PM.ProjectBaseline>(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── BaselineChangeRequest ─────────────────────────────────────────────
        modelBuilder.Entity<PM.BaselineChangeRequest>(entity =>
        {
            entity.ToTable("BaselineChangeRequests");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.RequestedByUserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.RequestedByUserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.ChangeJustification)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(e => e.ProposedChangesJson)
                  .IsRequired()
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Status)
                  .HasConversion<int>()
                  .HasDefaultValue(PM.ChangeRequestStatus.Pending);

            entity.Property(e => e.ReviewedByUserId)
                  .HasMaxLength(450);

            entity.Property(e => e.ReviewedByUserName)
                  .HasMaxLength(256);

            entity.Property(e => e.ReviewNotes)
                  .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Project)
                  .WithMany(p => p.ChangeRequests)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Restrict);
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

        // ActionItem no longer inherits BaseEntity — handle UpdatedAt separately
        foreach (var entry in ChangeTracker.Entries<ActionItem>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
