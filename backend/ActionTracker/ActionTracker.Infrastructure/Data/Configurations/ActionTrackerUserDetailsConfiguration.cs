using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class ActionTrackerUserDetailsConfiguration : IEntityTypeConfiguration<ActionTrackerUserDetails>
{
    public void Configure(EntityTypeBuilder<ActionTrackerUserDetails> builder)
    {
        builder.ToTable("ActionTrackerUserDetails");

        // UserId is both the PK and the FK to AspNetUsers — no separate identity column.
        builder.HasKey(d => d.UserId);

        builder.Property(d => d.UserId)
            .HasColumnType("nvarchar(450)");

        builder.Property(d => d.UserName)
            .HasMaxLength(256);

        builder.Property(d => d.Email)
            .HasMaxLength(256);

        builder.Property(d => d.EmpId)
            .HasMaxLength(500);

        builder.Property(d => d.DepartmentName)
            .HasMaxLength(200);

        builder.Property(d => d.UnitName)
            .HasMaxLength(200);

        builder.Property(d => d.SectionName)
            .HasMaxLength(200);

        builder.Property(d => d.TeamName)
            .HasMaxLength(200);

        builder.Property(d => d.ManagerId)
            .HasMaxLength(500);

        builder.Property(d => d.ManagerName)
            .HasMaxLength(256);

        // One-to-one: ApplicationUser <-> ActionTrackerUserDetails
        builder.HasOne(d => d.User)
            .WithOne(u => u.UserDetails)
            .HasForeignKey<ActionTrackerUserDetails>(d => d.UserId)
            .HasPrincipalKey<ApplicationUser>(u => u.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
