// One-off CLI tool: bulk-register every row from [dbo].[ku_employee_info]
// as an internal (Azure AD) ApplicationUser with the "Team Member" role.
//
// Usage (from this project's folder):
//     dotnet run
//
// Optional CLI override for the connection string:
//     dotnet run -- --connection-string "Server=...;Database=...;..."
//
// Behaviour:
//   * Skips rows where EmailAddress is null or whitespace.
//   * Skips emails that already exist in AspNetUsers (case-insensitive).
//   * Newly-created users get LoginProvider=AzureAD, EmailConfirmed=true, no password.
//   * Every new user is assigned the "Team Member" role.

using ActionTracker.Domain.Constants;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Pull the connection string from the API's appsettings.json so we don't keep
// a second copy. Path is resolved relative to the build output so it works
// under `dotnet run` and `dotnet exec` alike.
var apiAppsettings = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..",
    "ActionTracker.API", "appsettings.json"));

if (File.Exists(apiAppsettings))
{
    builder.Configuration.AddJsonFile(apiAppsettings, optional: false, reloadOnChange: false);
}

// Allow `--connection-string "..."` to override the resolved config value.
builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--connection-string"] = "ConnectionStrings:DefaultConnection",
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "ERROR: No connection string found. Looked at:\n" +
        $"  {apiAppsettings}\n" +
        "Pass one with: dotnet run -- --connection-string \"...\"");
    return 1;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Mirror the API's password rules (irrelevant here — we never set a password,
        // but Identity validates options at startup).
        options.Password.RequiredLength         = 8;
        options.Password.RequireDigit           = true;
        options.Password.RequireUppercase       = true;
        options.Password.RequireLowercase       = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
    // No .AddDefaultTokenProviders() — token providers need IDataProtectionProvider
    // which isn't registered by the generic console host, and this tool never issues
    // password-reset / email-confirmation tokens (it only calls CreateAsync without a
    // password and AddToRoleAsync).

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine        = true;
    o.TimestampFormat   = "HH:mm:ss ";
    o.IncludeScopes     = false;
});

using var host = builder.Build();

await using var scope = host.Services.CreateAsyncScope();
var sp          = scope.ServiceProvider;
var logger      = sp.GetRequiredService<ILogger<Program>>();
var db          = sp.GetRequiredService<AppDbContext>();
var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

const string targetRole = AppRoles.TeamMember;

if (!await roleManager.RoleExistsAsync(targetRole))
{
    Console.Error.WriteLine(
        $"ERROR: Role '{targetRole}' does not exist. Start the API once to run RoleSeeder, then re-run this tool.");
    return 2;
}

logger.LogInformation("Loading employees from ku_employee_info...");

// Project to just the columns we need, deduplicate by email (case-insensitive),
// and only keep rows with a non-empty email.
var candidates = await db.KuEmployeeInfo
    .AsNoTracking()
    .Where(e => e.EmailAddress != null && e.EmailAddress != "")
    .Select(e => new
    {
        e.EmailAddress,
        e.EmployeeName,
        e.FirstName,
        e.LastName,
        e.Department,
        e.Position,
        e.EmpNo,
    })
    .ToListAsync();

logger.LogInformation("Loaded {Count} employee rows with a non-empty email.", candidates.Count);

var existingEmails = await userManager.Users
    .Where(u => u.Email != null)
    .Select(u => u.Email!)
    .ToListAsync();

var existingEmailSet = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);
var seenInThisRun    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

int created = 0, skippedAlreadyRegistered = 0, skippedDuplicateInRun = 0, failed = 0;

foreach (var emp in candidates)
{
    var email = emp.EmailAddress!.Trim();
    if (email.Length == 0) continue;

    if (existingEmailSet.Contains(email))
    {
        skippedAlreadyRegistered++;
        continue;
    }

    if (!seenInThisRun.Add(email))
    {
        // ku_employee_info has multiple rows per person (assignment history).
        skippedDuplicateInRun++;
        continue;
    }

    var (firstName, lastName) = SplitName(emp.FirstName, emp.LastName, emp.EmployeeName);

    var user = new ApplicationUser
    {
        UserName       = email,
        Email          = email,
        FirstName      = firstName,
        LastName       = lastName,
        Department     = emp.Department ?? string.Empty,
        LoginProvider  = "AzureAD",
        IsActive       = true,
        EmailConfirmed = true,
        CreatedAt      = DateTime.UtcNow,
        CreatedBy      = "BulkImportEmployees",
    };

    var createResult = await userManager.CreateAsync(user);
    if (!createResult.Succeeded)
    {
        failed++;
        logger.LogWarning("Failed to create {Email}: {Errors}",
            email, string.Join("; ", createResult.Errors.Select(e => e.Description)));
        continue;
    }

    var roleResult = await userManager.AddToRoleAsync(user, targetRole);
    if (!roleResult.Succeeded)
    {
        failed++;
        logger.LogWarning("User {Email} created but role assignment failed: {Errors}",
            email, string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        continue;
    }

    created++;
    if (created % 50 == 0)
        logger.LogInformation("Progress: {Created} created so far...", created);
}

logger.LogInformation(
    "Done. Created: {Created}, AlreadyRegistered: {AlreadyRegistered}, DuplicateInRun: {DuplicateInRun}, Failed: {Failed}, TotalEmployeesScanned: {Total}",
    created, skippedAlreadyRegistered, skippedDuplicateInRun, failed, candidates.Count);

return 0;

// ── Helpers ─────────────────────────────────────────────────────────────────

static (string firstName, string lastName) SplitName(string? first, string? last, string? fullName)
{
    if (!string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(last))
        return (first?.Trim() ?? string.Empty, last?.Trim() ?? string.Empty);

    var trimmed = (fullName ?? string.Empty).Trim();
    if (trimmed.Length == 0) return (string.Empty, string.Empty);

    var space = trimmed.IndexOf(' ');
    return space < 0
        ? (trimmed, string.Empty)
        : (trimmed[..space], trimmed[(space + 1)..]);
}
