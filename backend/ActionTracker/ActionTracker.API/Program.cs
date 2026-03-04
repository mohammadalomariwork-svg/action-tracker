using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Extensions;
using ActionTracker.API.Middleware;
using ActionTracker.Application.Features.ActionItems.Validators;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Helpers;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Aliases for the new auth contract (avoids ambiguity with Interfaces.IAuthService)
using INewAuthService = ActionTracker.Application.Features.Auth.IAuthService;
using NewAuthService  = ActionTracker.Infrastructure.Features.Auth.AuthService;

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
    // Identity
    // -----------------------------------------------------------------------
    builder.Services.AddIdentityConfiguration(builder.Configuration);

    // -----------------------------------------------------------------------
    // JWT Bearer Authentication
    // Reads Jwt:Key, Jwt:Issuer, Jwt:Audience from configuration.
    // No secrets are hardcoded.
    // -----------------------------------------------------------------------
    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var key = builder.Configuration["Jwt:Key"]
                      ?? throw new InvalidOperationException(
                          "Jwt:Key is required but was not found in configuration.");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(
                                               Encoding.UTF8.GetBytes(key)),
                ValidateIssuer           = true,
                ValidIssuer              = builder.Configuration["Jwt:Issuer"],
                ValidateAudience         = true,
                ValidAudience            = builder.Configuration["Jwt:Audience"],
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero,
            };
        });

    // -----------------------------------------------------------------------
    // Authorization policies
    //   RequireAdmin   — Admin role only
    //   RequireManager — Admin or Manager role
    //   RequireUser    — any authenticated user
    // -----------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdmin",
            policy => policy.RequireRole("Admin"));

        options.AddPolicy("RequireManager",
            policy => policy.RequireRole("Admin", "Manager"));

        options.AddPolicy("RequireUser",
            policy => policy.RequireAuthenticatedUser());
    });

    // -----------------------------------------------------------------------
    // CORS — "AllowAngularApp"
    // Origins are read from configuration key "AllowedOrigins".
    // -----------------------------------------------------------------------
    builder.Services.AddCors(options =>
        options.AddPolicy("AllowAngularApp", policy =>
            policy
                .WithOrigins(
                    builder.Configuration
                        .GetSection("AllowedOrigins")
                        .Get<string[]>() ?? Array.Empty<string>())
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));

    // -----------------------------------------------------------------------
    // Auth helpers and service — explicit registrations in Program.cs
    // (AddApplicationServices below also registers these; last-write wins
    //  in ASP.NET Core DI but both resolve to the same types, so harmless)
    // -----------------------------------------------------------------------
    builder.Services.AddScoped<JwtTokenHelper>();
    builder.Services.AddScoped<INewAuthService, NewAuthService>();

    // -----------------------------------------------------------------------
    // Application services, Swagger
    // -----------------------------------------------------------------------
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
    //   4. CORS            — must come before auth so preflight requests pass
    //   5. Authentication  — must come before Authorization
    //   6. Authorization
    //   7. Swagger / Controllers
    // -----------------------------------------------------------------------
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<ExceptionMiddleware>();

    app.UseHttpsRedirection();
    app.UseCors("AllowAngularApp");

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Action Tracker API v1");
        options.RoutePrefix = "swagger";
    });

    // Redirect root to Swagger UI so opening the app shows the API docs
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

    app.MapControllers();

    // -----------------------------------------------------------------------
    // Auto-migrate: create / update the database schema on startup
    // -----------------------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

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

        const string adminEmail    = "Admin@action-tracker.com";
        const string adminPassword = "Test@4321";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName    = "Admin",
                Email       = adminEmail,
                DisplayName = "Admin",
                FirstName   = "Admin",
                LastName    = string.Empty,
                Role        = "Admin",
                IsActive    = true,
                CreatedAt   = DateTime.UtcNow,
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
