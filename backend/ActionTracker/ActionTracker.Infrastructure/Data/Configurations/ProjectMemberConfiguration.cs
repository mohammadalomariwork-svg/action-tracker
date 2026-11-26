using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

/// <summary>
/// Project people (WO-2026-11 P-2): additional project managers, viewers and team
/// members. No query filter of its own: the private-workspace filters read this
/// table (through <c>AppDbContext.ProjectRoleProjectIds</c>) to decide what a user
/// may see, and a filter here would make that lookup circular.
/// </summary>
public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnType("uniqueidentifier")
            .HasDefaultValueSql("NEWID()")
            .ValueGeneratedOnAdd();

        builder.Property(m => m.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(m => m.Role)
            .HasConversion<int>();

        builder.Property(m => m.AddedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(m => m.AddedByUserId)
            .HasMaxLength(450);

        // One row per person and role on a project.
        builder.HasIndex(m => new { m.ProjectId, m.UserId, m.Role })
            .IsUnique();

        // The access lookup reads "projects where this user has a role".
        builder.HasIndex(m => m.UserId);

        // Projects are only soft-deleted; cascade mirrors ProjectSponsors.
        builder.HasOne(m => m.Project)
            .WithMany(p => p.People)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
