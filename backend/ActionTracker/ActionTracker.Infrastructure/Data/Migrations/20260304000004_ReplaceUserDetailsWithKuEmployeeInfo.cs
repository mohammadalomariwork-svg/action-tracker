using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActionTracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceUserDetailsWithKuEmployeeInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the previous ActionTrackerUserDetails table
            migrationBuilder.DropTable(name: "ActionTrackerUserDetails");

            // Create the HR employee info table matching [dbo].[ku_employee_info]
            migrationBuilder.CreateTable(
                name: "ku_employee_info",
                columns: table => new
                {
                    AssignmentId       = table.Column<long>   (type: "numeric(18, 0)", nullable: false),
                    EmpNo              = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    PersonID           = table.Column<long?>  (type: "numeric(18, 0)", nullable: true),
                    EBSEmployeeNumber  = table.Column<string> (type: "nvarchar(256)",  maxLength: 256,  nullable: true),
                    EBSPersonId        = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    EmployeeName       = table.Column<string> (type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EmployeeArabicName = table.Column<string> (type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Position           = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    PositionName       = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    PositionCode       = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    PositionAR         = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Job                = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    OrgUnit            = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    OrgUnitAR          = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Payroll            = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    Grade              = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Nationality        = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    ArCountry          = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    NatCat             = table.Column<string> (type: "varchar(100)",   maxLength: 100,  nullable: false, unicode: false),
                    DateOfBirth        = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    Age                = table.Column<int?>   (type: "int",            nullable: true),
                    EmailAddress       = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    EmiratesID         = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    MaritalStat        = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    Gender             = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    NearestAirport     = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: false),
                    Religion           = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    Title              = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    ORIGINALHIREDATE   = table.Column<DateTime?>(type: "datetime2",    nullable: true),
                    HireDate           = table.Column<DateTime> (type: "datetime2",    nullable: false),
                    ServiceYrs         = table.Column<long?>  (type: "numeric(10, 0)", nullable: true),
                    AdjustedServiceDate= table.Column<DateTime?>(type: "datetime2",   nullable: true),
                    SupervisorNumber   = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    SupervisorName     = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    AssignmentCategory = table.Column<string> (type: "nvarchar(320)",  maxLength: 320,  nullable: true),
                    Section            = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    SectionAR          = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Department         = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    DepartmentAR       = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Division           = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    DivisionAR         = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    Sector             = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    SectorAR           = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    College            = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    CollegeAcademic    = table.Column<string> (type: "nvarchar(960)",  maxLength: 960,  nullable: true),
                    SDivision          = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    VPID               = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    VPName             = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    SVPID              = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    SVPName            = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    Contract_End       = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    ContractStartDate  = table.Column<DateTime?>(type: "datetime2",    nullable: true),
                    AssignmentEnddate  = table.Column<DateTime> (type: "datetime2",    nullable: false),
                    Gross              = table.Column<double?> (type: "float",          nullable: true),
                    EffectiveEndDate   = table.Column<DateTime> (type: "datetime2",    nullable: false),
                    TerminationDate    = table.Column<DateTime?>(type: "datetime2",    nullable: true),
                    PersonType         = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    UserPersonType     = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    SystemPersonType   = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    ContractType       = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    FirstName          = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    MiddleName         = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    GrandFatherName    = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    LastName           = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    EmployeeCategory   = table.Column<string> (type: "nvarchar(255)",  maxLength: 255,  nullable: true),
                    EmployeeData       = table.Column<string> (type: "nvarchar(max)",  nullable: true),
                    LastUpdate         = table.Column<DateTime?>(type: "datetime",     nullable: true),
                    EmployeeData_Html  = table.Column<string> (type: "nvarchar(max)",  nullable: true),
                    LastUpdate_Html    = table.Column<DateTime?>(type: "datetime",     nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ku_employee_info", x => x.AssignmentId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ku_employee_info");

            // Restore ActionTrackerUserDetails
            migrationBuilder.CreateTable(
                name: "ActionTrackerUserDetails",
                columns: table => new
                {
                    UserId         = table.Column<string> (type: "nvarchar(450)", nullable: false),
                    UserName       = table.Column<string> (type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email          = table.Column<string> (type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmpId          = table.Column<string> (type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DepartmentId   = table.Column<int?>   (type: "int",           nullable: true),
                    DepartmentName = table.Column<string> (type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnitId         = table.Column<int?>   (type: "int",           nullable: true),
                    UnitName       = table.Column<string> (type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SectionId      = table.Column<int?>   (type: "int",           nullable: true),
                    SectionName    = table.Column<string> (type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TeamId         = table.Column<int?>   (type: "int",           nullable: true),
                    TeamName       = table.Column<string> (type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ManagerId      = table.Column<string> (type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ManagerName    = table.Column<string> (type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionTrackerUserDetails", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_ActionTrackerUserDetails_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}
