namespace ActionTracker.Application.Features.UserManagement.DTOs;

/// <summary>
/// Represents the current user's employee profile from the KU employee directory.
/// </summary>
public class EmployeeProfileDto
{
    // ── Identity ────────────────────────────────────────────────────────────
    public string? EmpNo              { get; set; }
    public string? EBSEmployeeNumber  { get; set; }

    // ── Names ───────────────────────────────────────────────────────────────
    public string? EmployeeName       { get; set; }
    public string? EmployeeArabicName { get; set; }
    public string? FirstName          { get; set; }
    public string? MiddleName         { get; set; }
    public string? LastName           { get; set; }
    public string? Title              { get; set; }

    // ── Contact ─────────────────────────────────────────────────────────────
    public string? EmailAddress       { get; set; }

    // ── Position / Job ──────────────────────────────────────────────────────
    public string? Position           { get; set; }
    public string? PositionName       { get; set; }
    public string? Job                { get; set; }
    public string? Grade              { get; set; }

    // ── Org structure ───────────────────────────────────────────────────────
    public string? OrgUnit            { get; set; }
    public string? Section            { get; set; }
    public string? Department         { get; set; }
    public string? Division           { get; set; }
    public string? Sector             { get; set; }
    public string? College            { get; set; }

    // ── VP / SVP ────────────────────────────────────────────────────────────
    public string? VPName             { get; set; }
    public string? SVPName            { get; set; }

    // ── Supervisor ──────────────────────────────────────────────────────────
    public string? SupervisorName     { get; set; }

    // ── Personal ────────────────────────────────────────────────────────────
    public string? Nationality        { get; set; }
    public string? Gender             { get; set; }

    // ── Employment ──────────────────────────────────────────────────────────
    public DateTime? OriginalHireDate { get; set; }
    public DateTime  HireDate         { get; set; }
    public long?     ServiceYrs       { get; set; }
    public string?   ContractType     { get; set; }
    public string?   EmployeeCategory { get; set; }
    public string?   PersonType       { get; set; }
}
