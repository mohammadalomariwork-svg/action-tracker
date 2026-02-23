using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Extensions;
using ActionTracker.API.Middleware;
using ActionTracker.Application.Features.ActionItems.Validators;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ---------------------------------------------------------------------------
// Bootstrap Serilog early so startup errors are captured
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // -----------------------------------------------------------------------
    // Serilog — reads full config from appsettings.json "Serilog" section
    // -----------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services));

    // -----------------------------------------------------------------------
    // Database
    // -----------------------------------------------------------------------
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions => sqlOptions.MigrationsAssembly(
                typeof(AppDbContext).Assembly.FullName)));

    // -----------------------------------------------------------------------
    // Identity, JWT, CORS, Application services, Swagger
    // -----------------------------------------------------------------------
    builder.Services.AddIdentityConfiguration(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddSwaggerWithJwt();

    // -----------------------------------------------------------------------
    // Controllers — JSON: camelCase + string enums
    // -----------------------------------------------------------------------
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.Converters.Add(
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.JsonSerializerOptions.DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull;
        });

    // -----------------------------------------------------------------------
    // FluentValidation — auto-validates all DTOs in ActionTracker.Application
    // -----------------------------------------------------------------------
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<ActionItemCreateValidator>();

    // -----------------------------------------------------------------------
    // Build
    // -----------------------------------------------------------------------
    var app = builder.Build();

    // -----------------------------------------------------------------------
    // Middleware pipeline (order matters)
    //   1. RequestLogging  — outermost; captures total elapsed time
    //   2. Exception       — catches all unhandled exceptions before response
    //   3. HttpsRedirection
    //   4. CORS
    //   5. Authentication / Authorization
    //   6. Controllers
    // -----------------------------------------------------------------------
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();

    app.UseHttpsRedirection();
    app.UseCors("ActionTrackerCors");

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Action Tracker API v1");
            options.RoutePrefix = "swagger";
        });
    }

    app.MapControllers();

    // -----------------------------------------------------------------------
    // Database seeding — create roles and default admin user if absent
    // -----------------------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var roleName in new[] { "Admin", "Manager", "TeamMember" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        const string adminEmail    = "admin@actiontracker.com";
        const string adminPassword = "Admin@2026!";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName   = adminEmail,
                Email      = adminEmail,
                FirstName  = "System",
                LastName   = "Admin",
                Role       = "Admin",
                IsActive   = true,
                CreatedAt  = DateTime.UtcNow,
            };

            var result = await userManager.CreateAsync(admin, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                Log.Information("Default admin user seeded: {Email}", adminEmail);
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                Log.Warning("Failed to seed admin user: {Errors}", errors);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Run
    // -----------------------------------------------------------------------
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Expose Program to the test project for WebApplicationFactory<Program>
public partial class Program { }
