using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Permissions;
using ActionTracker.Application.Permissions.Domain;
using ActionTracker.Domain.Common;
using ActionTracker.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// Alias the Projects models namespace to disambiguate from identically-named
// domain entities (ActionItem, StrategicObjective) that pre-exist in the schema.

namespace ActionTracker.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>, IAppDbContext
{
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Authenticated user of the current request, read by the private-workspace
    /// query filters. <c>null</c> = system context (background jobs, seeders,
    /// design-time) and disables the private-workspace restriction.
    /// </summary>
    public string? CurrentUserId => _currentUser?.UserId;

    // ── Legacy / shared domain sets ───────────────────────────────────────────
    public DbSet<ActionItem>           ActionItems           => Set<ActionItem>();
    public DbSet<ActionItemAssignee>   ActionItemAssignees   => Set<ActionItemAssignee>();
    public DbSet<ActionItemEscalation> ActionItemEscalations => Set<ActionItemEscalation>();
    public DbSet<ActionItemComment>    ActionItemComments    => Set<ActionItemComment>();
    public DbSet<Comment>              Comments              => Set<Comment>();
    public DbSet<Document>             Documents             => Set<Document>();
    public DbSet<RefreshToken>        RefreshTokens        => Set<RefreshToken>();
    public DbSet<KuEmployeeInfo>      KuEmployeeInfo       => Set<KuEmployeeInfo>();
    public DbSet<OrgUnit>             OrgUnits             => Set<OrgUnit>();
    public DbSet<StrategicObjective>  StrategicObjectives  => Set<StrategicObjective>();
    public DbSet<StrategicPlan>       StrategicPlans       => Set<StrategicPlan>();
    public DbSet<Kpi>                 Kpis                 => Set<Kpi>();
    public DbSet<KpiTarget>           KpiTargets           => Set<KpiTarget>();
    public DbSet<KpiActual>           KpiActuals           => Set<KpiActual>();
    public DbSet<Workspace>           Workspaces           => Set<Workspace>();
    public DbSet<WorkspaceAdmin>      WorkspaceAdmins      => Set<WorkspaceAdmin>();
    public DbSet<WorkspaceMember>     WorkspaceMembers     => Set<WorkspaceMember>();
    public DbSet<WorkspaceProjectManager>           WorkspaceProjectManagers           => Set<WorkspaceProjectManager>();
    public DbSet<WorkspaceStrategicProjectApprover> WorkspaceStrategicProjectApprovers => Set<WorkspaceStrategicProjectApprover>();

    // ── Permissions feature sets ──────────────────────────────────────────────
    public DbSet<RolePermission>         RolePermissions         => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AppPermissionArea>      PermissionAreas         => Set<AppPermissionArea>();
    public DbSet<AppPermissionAction>    PermissionActions       => Set<AppPermissionAction>();
    public DbSet<AreaPermissionMapping>  AreaPermissionMappings  => Set<AreaPermissionMapping>();

    // ── Projects feature sets ─────────────────────────────────────────────────
    // NOTE: PM.StrategicObjective (workspace-scoped, int PK) is a different
    //       entity from the domain StrategicObjective (admin panel, Guid PK).
    //       It maps to "WorkspaceStrategicObjectives" to avoid a table clash.
    public DbSet<Project>           Projects             => Set<Project>();
    public DbSet<ProjectSponsor>    ProjectSponsors      => Set<ProjectSponsor>();
    public DbSet<Milestone>         Milestones           => Set<Milestone>();
    public DbSet<ProjectRisk>       ProjectRisks         => Set<ProjectRisk>();

    // ── Email feature sets ───────────────────────────────────────────────────
    public DbSet<EmailTemplate>     EmailTemplates       => Set<EmailTemplate>();
    public DbSet<EmailLog>          EmailLogs            => Set<EmailLog>();
    public DbSet<MailServerConfig>  MailServerConfigs    => Set<MailServerConfig>();

    // ── Data sync ─────────────────────────────────────────────────────────────
    public DbSet<DataSyncJob>       DataSyncJobs         => Set<DataSyncJob>();
    public DbSet<DataSyncJobRun>    DataSyncJobRuns      => Set<DataSyncJobRun>();

    // ── Work calendars ────────────────────────────────────────────────────────
    public DbSet<WorkCalendar>          WorkCalendars          => Set<WorkCalendar>();
    public DbSet<WorkCalendarDay>       WorkCalendarDays       => Set<WorkCalendarDay>();
    public DbSet<WorkCalendarException> WorkCalendarExceptions => Set<WorkCalendarException>();

    // ── Notification feature sets ────────────────────────────────────────────
    public DbSet<AppNotification>   AppNotifications     => Set<AppNotification>();

    // ── Workflow feature sets ────────────────────────────────────────────────
    public DbSet<ActionItemWorkflowRequest> ActionItemWorkflowRequests => Set<ActionItemWorkflowRequest>();
    public DbSet<ProjectApprovalRequest> ProjectApprovalRequests => Set<ProjectApprovalRequest>();
    public DbSet<StrategicPlanApprovalRequest> StrategicPlanApprovalRequests => Set<StrategicPlanApprovalRequest>();
    public DbSet<StrategicPlanApprover> StrategicPlanApprovers => Set<StrategicPlanApprover>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ── Ensure all DateTime values are treated as UTC ────────────────────
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableDateTimeConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue && v.Value.Kind != DateTimeKind.Utc
                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(dateTimeConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableDateTimeConverter);
            }
        }

        // Private workspaces: only their admins + members can see the workspace
        // or anything under it. These filters make every query and aggregate
        // (dashboards, reports, stats, strategy roll-ups) skip private data for
        // everyone else. CurrentUserId == null (system context) is unrestricted.
        modelBuilder.Entity<Workspace>().HasQueryFilter(w =>
            CurrentUserId == null || !w.IsPrivate
            || w.Admins.Any(a => a.AdminUserId == CurrentUserId)
            || w.Members.Any(m => m.UserId == CurrentUserId));

        modelBuilder.Entity<ActionItem>().HasQueryFilter(e => !e.IsDeleted
            && (CurrentUserId == null || !e.Workspace.IsPrivate
                || e.Workspace.Admins.Any(a => a.AdminUserId == CurrentUserId)
                || e.Workspace.Members.Any(m => m.UserId == CurrentUserId)));
        modelBuilder.Entity<OrgUnit>().HasQueryFilter(o => !o.IsDeleted);
        // Drafts (IsDraft = true) are hidden from every "live" query — the
        // draft service uses IgnoreQueryFilters() to reach them. CR-001-G.
        modelBuilder.Entity<StrategicObjective>().HasQueryFilter(o => !o.IsDeleted && !o.IsDraft);
        modelBuilder.Entity<StrategicPlan>().HasQueryFilter(p => !p.IsDeleted && !p.IsDraft);
        modelBuilder.Entity<Kpi>().HasQueryFilter(o => !o.IsDeleted && !o.IsDraft);
        modelBuilder.Entity<KpiTarget>().HasQueryFilter(t => !t.IsDraft);
        modelBuilder.Entity<Project>().HasQueryFilter(p => !p.IsDeleted
            && (CurrentUserId == null || !p.Workspace.IsPrivate
                || p.Workspace.Admins.Any(a => a.AdminUserId == CurrentUserId)
                || p.Workspace.Members.Any(m => m.UserId == CurrentUserId)));
        modelBuilder.Entity<Milestone>().HasQueryFilter(m => !m.IsDeleted
            && (CurrentUserId == null || !m.Project.Workspace.IsPrivate
                || m.Project.Workspace.Admins.Any(a => a.AdminUserId == CurrentUserId)
                || m.Project.Workspace.Members.Any(wm => wm.UserId == CurrentUserId)));
        modelBuilder.Entity<EmailTemplate>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ProjectRisk>().HasQueryFilter(r => !r.IsDeleted
            && (CurrentUserId == null || !r.Project.Workspace.IsPrivate
                || r.Project.Workspace.Admins.Any(a => a.AdminUserId == CurrentUserId)
                || r.Project.Workspace.Members.Any(wm => wm.UserId == CurrentUserId)));
        modelBuilder.Entity<ProjectApprovalRequest>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<StrategicPlanApprovalRequest>().HasQueryFilter(r => !r.IsDeleted);
        modelBuilder.Entity<KpiActual>().HasQueryFilter(a => !a.IsDeleted);

        // ── Draft self-FKs + uniqueness invariants (CR-001-G) ────────────────
        modelBuilder.Entity<StrategicPlan>(e =>
        {
            e.HasOne(p => p.OriginalPlan)
             .WithMany()
             .HasForeignKey(p => p.OriginalPlanId)
             .OnDelete(DeleteBehavior.Restrict);
            // Filtered unique index: at most one open draft per published plan.
            e.HasIndex(p => p.OriginalPlanId)
             .IsUnique()
             .HasFilter("[OriginalPlanId] IS NOT NULL");
        });

        modelBuilder.Entity<StrategicObjective>(e =>
        {
            e.HasOne(o => o.OriginalObjective)
             .WithMany()
             .HasForeignKey(o => o.OriginalObjectiveId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(o => o.OriginalObjectiveId);
        });

        modelBuilder.Entity<Kpi>(e =>
        {
            e.HasOne(k => k.OriginalKpi)
             .WithMany()
             .HasForeignKey(k => k.OriginalKpiId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(k => k.OriginalKpiId);
        });

        modelBuilder.Entity<KpiTarget>(e =>
        {
            e.HasOne(t => t.OriginalKpiTarget)
             .WithMany()
             .HasForeignKey(t => t.OriginalKpiTargetId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => t.OriginalKpiTargetId);
        });

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

            entity.Property(e => e.IsPrivate)
                  .IsRequired();

            entity.HasIndex(e => e.OrganizationUnit);
            entity.HasIndex(e => e.StrategicPlanId);
            entity.HasIndex(e => e.WorkCalendarId);

            // Null = the workspace uses the default calendar. Restrict: a
            // calendar a workspace points at cannot be removed from under it.
            entity.HasOne(e => e.WorkCalendar)
                  .WithMany()
                  .HasForeignKey(e => e.WorkCalendarId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Admins)
                  .WithOne(a => a.Workspace)
                  .HasForeignKey(a => a.WorkspaceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Members)
                  .WithOne(m => m.Workspace)
                  .HasForeignKey(m => m.WorkspaceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.ProjectManagers)
                  .WithOne(p => p.Workspace)
                  .HasForeignKey(p => p.WorkspaceId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.StrategicProjectApprovers)
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

        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.ToTable("WorkspaceMembers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                  .UseIdentityColumn();

            entity.Property(e => e.UserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.UserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.AddedAt)
                  .IsRequired()
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.AddedByUserId)
                  .HasMaxLength(450);

            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<WorkspaceProjectManager>(entity =>
        {
            entity.ToTable("WorkspaceProjectManagers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.UserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.UserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.AddedAt)
                  .IsRequired()
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.AddedByUserId)
                  .HasMaxLength(450);

            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
        });

        modelBuilder.Entity<WorkspaceStrategicProjectApprover>(entity =>
        {
            entity.ToTable("WorkspaceStrategicProjectApprovers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityColumn();

            entity.Property(e => e.UserId)
                  .IsRequired()
                  .HasMaxLength(450);

            entity.Property(e => e.UserName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.Property(e => e.Order)
                  .IsRequired();

            entity.Property(e => e.AddedAt)
                  .IsRequired()
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.AddedByUserId)
                  .HasMaxLength(450);

            entity.HasIndex(e => e.WorkspaceId);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.WorkspaceId, e.Order });
        });

        modelBuilder.Entity<ProjectApprovalRequest>(entity =>
        {
            entity.Property(e => e.AssignedReviewerUserId).HasMaxLength(450);
            entity.Property(e => e.AssignedReviewerDisplayName).HasMaxLength(256);
            entity.Property(e => e.Level).HasDefaultValue(1);
            entity.Property(e => e.TotalLevels).HasDefaultValue(1);
            entity.HasIndex(e => e.AssignedReviewerUserId);
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

        foreach (var entry in ChangeTracker.Entries<Project>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Milestone>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ProjectRisk>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        foreach (var entry in ChangeTracker.Entries<EmailTemplate>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
