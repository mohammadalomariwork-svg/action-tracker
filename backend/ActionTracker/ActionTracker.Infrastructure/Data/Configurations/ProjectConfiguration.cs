using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.ProjectCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(p => p.ProjectCode)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        // Unique name within a workspace
        builder.HasIndex(p => new { p.WorkspaceId, p.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.Property(p => p.Description)
            .HasMaxLength(5000);

        builder.Property(p => p.ProjectType)
            .HasConversion<int>();

        builder.Property(p => p.Status)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.ProjectStatus.Draft);

        builder.Property(p => p.Priority)
            .HasConversion<int>();

        builder.Property(p => p.ProjectManagerUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.ApprovedBudget)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.Currency)
            .IsRequired()
            .HasMaxLength(10)
            .HasDefaultValue("AED");

        builder.Property(p => p.IsBaselined)
            .HasDefaultValue(false);

        builder.Property(p => p.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(p => p.CreatedBy)
            .HasMaxLength(450);

        // FK to Workspace
        builder.HasOne(p => p.Workspace)
            .WithMany()
            .HasForeignKey(p => p.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to StrategicObjective (optional)
        builder.HasOne(p => p.StrategicObjective)
            .WithMany()
            .HasForeignKey(p => p.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to ProjectManager (ApplicationUser)
        builder.HasOne(p => p.ProjectManager)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerUserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to OrgUnit (optional)
        builder.HasOne(p => p.OwnerOrgUnit)
            .WithMany()
            .HasForeignKey(p => p.OwnerOrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.WorkspaceId);
        builder.HasIndex(p => p.Status);
    }
}

public class ProjectSponsorConfiguration : IEntityTypeConfiguration<ProjectSponsor>
{
    public void Configure(EntityTypeBuilder<ProjectSponsor> builder)
    {
        builder.ToTable("ProjectSponsors");

        builder.HasKey(ps => new { ps.ProjectId, ps.UserId });

        builder.Property(ps => ps.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(ps => ps.Project)
            .WithMany(p => p.Sponsors)
            .HasForeignKey(ps => ps.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ps => ps.User)
            .WithMany()
            .HasForeignKey(ps => ps.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
