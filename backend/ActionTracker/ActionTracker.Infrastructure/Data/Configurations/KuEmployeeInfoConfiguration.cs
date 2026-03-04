using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ActionTracker.Infrastructure.Data.Configurations;

public class KuEmployeeInfoConfiguration : IEntityTypeConfiguration<KuEmployeeInfo>
{
    public void Configure(EntityTypeBuilder<KuEmployeeInfo> builder)
    {
        builder.ToTable("ku_employee_info");

        // AssignmentId is the PK — sourced externally, never auto-generated.
        builder.HasKey(e => e.AssignmentId);
        builder.Property(e => e.AssignmentId)
            .HasColumnType("numeric(18, 0)")
            .ValueGeneratedNever();

        // ── numeric types ────────────────────────────────────────────────────
        builder.Property(e => e.PersonID)   .HasColumnType("numeric(18, 0)");
        builder.Property(e => e.ServiceYrs) .HasColumnType("numeric(10, 0)");

        // ── varchar (non-unicode) ────────────────────────────────────────────
        builder.Property(e => e.NatCat)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(100);

        // ── required nvarchar columns ────────────────────────────────────────
        builder.Property(e => e.NearestAirport).IsRequired().HasMaxLength(255);

        // ── string lengths ───────────────────────────────────────────────────
        builder.Property(e => e.EmpNo)             .HasMaxLength(255);
        builder.Property(e => e.EBSEmployeeNumber) .HasMaxLength(256);
        builder.Property(e => e.EBSPersonId)       .HasMaxLength(255);
        builder.Property(e => e.EmployeeName)      .HasMaxLength(4000);
        builder.Property(e => e.EmployeeArabicName).HasMaxLength(4000);
        builder.Property(e => e.FirstName)         .HasMaxLength(255);
        builder.Property(e => e.MiddleName)        .HasMaxLength(255);
        builder.Property(e => e.GrandFatherName)   .HasMaxLength(255);
        builder.Property(e => e.LastName)          .HasMaxLength(255);
        builder.Property(e => e.Title)             .HasMaxLength(255);
        builder.Property(e => e.Position)          .HasMaxLength(960);
        builder.Property(e => e.PositionName)      .HasMaxLength(960);
        builder.Property(e => e.PositionCode)      .HasMaxLength(255);
        builder.Property(e => e.PositionAR)        .HasMaxLength(960);
        builder.Property(e => e.Job)               .HasMaxLength(960);
        builder.Property(e => e.OrgUnit)           .HasMaxLength(960);
        builder.Property(e => e.OrgUnitAR)         .HasMaxLength(960);
        builder.Property(e => e.Section)           .HasMaxLength(960);
        builder.Property(e => e.SectionAR)         .HasMaxLength(960);
        builder.Property(e => e.Department)        .HasMaxLength(960);
        builder.Property(e => e.DepartmentAR)      .HasMaxLength(960);
        builder.Property(e => e.Division)          .HasMaxLength(960);
        builder.Property(e => e.DivisionAR)        .HasMaxLength(960);
        builder.Property(e => e.SDivision)         .HasMaxLength(255);
        builder.Property(e => e.Sector)            .HasMaxLength(255);
        builder.Property(e => e.SectorAR)          .HasMaxLength(960);
        builder.Property(e => e.College)           .HasMaxLength(960);
        builder.Property(e => e.CollegeAcademic)   .HasMaxLength(960);
        builder.Property(e => e.Payroll)           .HasMaxLength(255);
        builder.Property(e => e.Grade)             .HasMaxLength(960);
        builder.Property(e => e.VPID)              .HasMaxLength(255);
        builder.Property(e => e.VPName)            .HasMaxLength(255);
        builder.Property(e => e.SVPID)             .HasMaxLength(255);
        builder.Property(e => e.SVPName)           .HasMaxLength(255);
        builder.Property(e => e.SupervisorNumber)  .HasMaxLength(255);
        builder.Property(e => e.SupervisorName)    .HasMaxLength(255);
        builder.Property(e => e.Nationality)       .HasMaxLength(255);
        builder.Property(e => e.ArCountry)         .HasMaxLength(255);
        builder.Property(e => e.DateOfBirth)       .HasMaxLength(255);
        builder.Property(e => e.EmailAddress)      .HasMaxLength(255);
        builder.Property(e => e.EmiratesID)        .HasMaxLength(255);
        builder.Property(e => e.MaritalStat)       .HasMaxLength(255);
        builder.Property(e => e.Gender)            .HasMaxLength(255);
        builder.Property(e => e.Religion)          .HasMaxLength(255);
        builder.Property(e => e.AssignmentCategory).HasMaxLength(320);
        builder.Property(e => e.PersonType)        .HasMaxLength(255);
        builder.Property(e => e.UserPersonType)    .HasMaxLength(255);
        builder.Property(e => e.SystemPersonType)  .HasMaxLength(255);
        builder.Property(e => e.ContractType)      .HasMaxLength(255);
        builder.Property(e => e.EmployeeCategory)  .HasMaxLength(255);

        // ── column name overrides (C# name differs from SQL column name) ─────
        builder.Property(e => e.OriginalHireDate)
            .HasColumnName("ORIGINALHIREDATE");

        builder.Property(e => e.ContractEnd)
            .HasColumnName("Contract_End")
            .HasMaxLength(255);

        builder.Property(e => e.EmployeeDataHtml)
            .HasColumnName("EmployeeData_Html");

        builder.Property(e => e.LastUpdateHtml)
            .HasColumnName("LastUpdate_Html")
            .HasColumnType("datetime");

        // ── legacy datetime (not datetime2) ──────────────────────────────────
        builder.Property(e => e.LastUpdate).HasColumnType("datetime");

        // ── float ────────────────────────────────────────────────────────────
        builder.Property(e => e.Gross).HasColumnType("float");
    }
}
