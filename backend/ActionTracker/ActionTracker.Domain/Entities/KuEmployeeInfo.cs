namespace ActionTracker.Domain.Entities;

/// <summary>
/// Mirror of [dbo].[ku_employee_info] — the HR employee data table
/// populated by the external Oracle EBS ETL process.
/// <para>
/// AssignmentId is used as the primary key (the only non-nullable
/// numeric identifier in the original schema).
/// </para>
/// </summary>
public class KuEmployeeInfo
{
    // ── Identity / IDs ────────────────────────────────────────────────────────

    public long    AssignmentId      { get; set; }   // numeric(18,0) NOT NULL — PK
    public string? EmpNo             { get; set; }   // nvarchar(255)
    public long?   PersonID          { get; set; }   // numeric(18,0)
    public string? EBSEmployeeNumber { get; set; }   // nvarchar(256)
    public string? EBSPersonId       { get; set; }   // nvarchar(255)

    // ── Names ─────────────────────────────────────────────────────────────────

    public string? EmployeeName      { get; set; }   // nvarchar(4000)
    public string? EmployeeArabicName{ get; set; }   // nvarchar(4000)
    public string? FirstName         { get; set; }   // nvarchar(255)
    public string? MiddleName        { get; set; }   // nvarchar(255)
    public string? GrandFatherName   { get; set; }   // nvarchar(255)
    public string? LastName          { get; set; }   // nvarchar(255)
    public string? Title             { get; set; }   // nvarchar(255)

    // ── Position / Job ────────────────────────────────────────────────────────

    public string? Position          { get; set; }   // nvarchar(960)
    public string? PositionName      { get; set; }   // nvarchar(960)
    public string? PositionCode      { get; set; }   // nvarchar(255)
    public string? PositionAR        { get; set; }   // nvarchar(960)
    public string? Job               { get; set; }   // nvarchar(960)

    // ── Org structure ─────────────────────────────────────────────────────────

    public string? OrgUnit           { get; set; }   // nvarchar(960)
    public string? OrgUnitAR         { get; set; }   // nvarchar(960)
    public string? Section           { get; set; }   // nvarchar(960)
    public string? SectionAR         { get; set; }   // nvarchar(960)
    public string? Department        { get; set; }   // nvarchar(960)
    public string? DepartmentAR      { get; set; }   // nvarchar(960)
    public string? Division          { get; set; }   // nvarchar(960)
    public string? DivisionAR        { get; set; }   // nvarchar(960)
    public string? SDivision         { get; set; }   // nvarchar(255)
    public string? Sector            { get; set; }   // nvarchar(255)
    public string? SectorAR          { get; set; }   // nvarchar(960)
    public string? College           { get; set; }   // nvarchar(960)
    public string? CollegeAcademic   { get; set; }   // nvarchar(960)
    public string? Payroll           { get; set; }   // nvarchar(255)
    public string? Grade             { get; set; }   // nvarchar(960)

    // ── VP / SVP ──────────────────────────────────────────────────────────────

    public string? VPID              { get; set; }   // nvarchar(255)
    public string? VPName            { get; set; }   // nvarchar(255)
    public string? SVPID             { get; set; }   // nvarchar(255)
    public string? SVPName           { get; set; }   // nvarchar(255)

    // ── Supervisor ────────────────────────────────────────────────────────────

    public string? SupervisorNumber  { get; set; }   // nvarchar(255)
    public string? SupervisorName    { get; set; }   // nvarchar(255)

    // ── Personal / demographics ───────────────────────────────────────────────

    public string? Nationality       { get; set; }   // nvarchar(255)
    public string? ArCountry         { get; set; }   // nvarchar(255)
    public string  NatCat            { get; set; } = string.Empty; // varchar(100) NOT NULL
    public string? DateOfBirth       { get; set; }   // nvarchar(255)
    public int?    Age               { get; set; }   // int
    public string? EmailAddress      { get; set; }   // nvarchar(255)
    public string? EmiratesID        { get; set; }   // nvarchar(255)
    public string? MaritalStat       { get; set; }   // nvarchar(255)
    public string? Gender            { get; set; }   // nvarchar(255)
    public string  NearestAirport    { get; set; } = string.Empty; // nvarchar(255) NOT NULL
    public string? Religion          { get; set; }   // nvarchar(255)

    // ── Employment dates ──────────────────────────────────────────────────────

    /// <summary>Maps to column [ORIGINALHIREDATE].</summary>
    public DateTime? OriginalHireDate   { get; set; }  // datetime2(7)
    public DateTime  HireDate           { get; set; }  // datetime2(7) NOT NULL
    public long?     ServiceYrs         { get; set; }  // numeric(10,0)
    public DateTime? AdjustedServiceDate{ get; set; }  // datetime2(7)
    public DateTime? ContractStartDate  { get; set; }  // datetime2(7)
    /// <summary>Maps to column [Contract_End].</summary>
    public string?   ContractEnd        { get; set; }  // nvarchar(255)
    public DateTime  AssignmentEnddate  { get; set; }  // datetime2(7) NOT NULL
    public DateTime  EffectiveEndDate   { get; set; }  // datetime2(7) NOT NULL
    public DateTime? TerminationDate    { get; set; }  // datetime2(7)

    // ── Assignment / contract ─────────────────────────────────────────────────

    public string? AssignmentCategory { get; set; }  // nvarchar(320)
    public string? PersonType         { get; set; }  // nvarchar(255)
    public string? UserPersonType     { get; set; }  // nvarchar(255)
    public string? SystemPersonType   { get; set; }  // nvarchar(255)
    public string? ContractType       { get; set; }  // nvarchar(255)
    public string? EmployeeCategory   { get; set; }  // nvarchar(255)

    // ── Compensation ──────────────────────────────────────────────────────────

    public double? Gross              { get; set; }  // float

    // ── ETL metadata ──────────────────────────────────────────────────────────

    public string?   EmployeeData     { get; set; }  // nvarchar(max)
    public DateTime? LastUpdate       { get; set; }  // datetime
    /// <summary>Maps to column [EmployeeData_Html].</summary>
    public string?   EmployeeDataHtml { get; set; }  // nvarchar(max)
    /// <summary>Maps to column [LastUpdate_Html].</summary>
    public DateTime? LastUpdateHtml   { get; set; }  // datetime
}
