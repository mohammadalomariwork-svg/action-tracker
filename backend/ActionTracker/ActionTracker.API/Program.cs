using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ActionTracker.API.Converters;
using ActionTracker.API.Extensions;
using ActionTracker.API.Middleware;
using ActionTracker.Application.Features.ActionItems.Validators;
using ActionTracker.Domain.Entities;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Data.Seeders;
using ActionTracker.Infrastructure.Helpers;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ActionTracker.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
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
    var azureTenantId = builder.Configuration["AzureAd:TenantId"];
    var azureAdEnabled = !string.IsNullOrWhiteSpace(azureTenantId);

    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "MultiAuth";
        options.DefaultChallengeScheme = "MultiAuth";
    })
    .AddJwtBearer("LocalBearer", options =>
    {
        // Keep classic sub → ClaimTypes.NameIdentifier remapping.
        // .NET 9's JsonWebTokenHandler no longer does this by default.
        options.MapInboundClaims = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR sends the JWT as ?access_token=... on WebSocket handshakes
        // because browsers can't attach an Authorization header to a WS upgrade.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

    if (azureAdEnabled)
    {
        var azureClientId = builder.Configuration["AzureAd:ClientId"]!;
        authBuilder.AddJwtBearer("AzureAD", options =>
        {
            options.Authority =
                $"https://login.microsoftonline.com/{azureTenantId}/v2.0";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[]
                {
                    $"https://login.microsoftonline.com/{azureTenantId}/v2.0",
                    $"https://sts.windows.net/{azureTenantId}/"
                },
                ValidateAudience = true,
                ValidAudiences = new[] { azureClientId, $"api://{azureClientId}" },
                ValidateLifetime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) &&
                        path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
    }

    authBuilder.AddPolicyScheme("MultiAuth", "MultiAuth", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            if (azureAdEnabled)
            {
                string? token = null;

                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader?.StartsWith("Bearer ") == true)
                {
                    token = authHeader["Bearer ".Length..].Trim();
                }
                else if (context.Request.Path.StartsWithSegments("/hubs"))
                {
                    // WebSocket handshakes can't set Authorization; SignalR
                    // passes the JWT as ?access_token=... instead.
                    token = context.Request.Query["access_token"].FirstOrDefault();
                }

                if (!string.IsNullOrEmpty(token))
                {
                    var jwtHandler = new JwtSecurityTokenHandler();
                    if (jwtHandler.CanReadToken(token))
                    {
                        var jwt = jwtHandler.ReadJwtToken(token);
                        if (jwt.Claims.Any(c => c.Type == "tid"))
                            return "AzureAD";
                    }
                }
            }
            return "LocalBearer";
        };
    });

    // -----------------------------------------------------------------------
    // Authorization policies
    // -----------------------------------------------------------------------
    builder.Services.AddAuthorization(options =>
    {
        var schemes = azureAdEnabled
            ? new[] { "LocalBearer", "AzureAD" }
            : new[] { "LocalBearer" };

        options.DefaultPolicy = new AuthorizationPolicyBuilder(schemes)
            .RequireAuthenticatedUser()
            .Build();
        options.AddPolicy("LocalOrAzureAD", policy =>
        {
            policy.AddAuthenticationSchemes(schemes);
            policy.RequireAuthenticatedUser();
        });
        options.AddPolicy("AdminOnly", policy =>
        {
            policy.AddAuthenticationSchemes(schemes);
            policy.RequireRole("Admin");
        });
        options.AddPolicy("AdminOrManager", policy =>
        {
            policy.AddAuthenticationSchemes(schemes);
            policy.RequireRole("Admin", "Manager");
        });
        options.AddPolicy("ManagerOrAdmin", policy =>
        {
            policy.AddAuthenticationSchemes(schemes);
            policy.RequireRole("Admin", "Manager");
        });

        // ── Fine-grained permission policies ─────────────────────────────────
        // Discover every public string constant from PermissionPolicies and
        // register a policy whose name is the constant value ("Area.Action").
        var policyNames = typeof(PermissionPolicies)
            .GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => f.GetRawConstantValue() as string)
            .OfType<string>();

        foreach (var policyName in policyNames)
        {
            var parts = policyName.Split('.');
            if (parts.Length != 2) continue;
            var area   = parts[0];
            var action = parts[1];
            options.AddPolicy(policyName, policy =>
            {
                policy.AddAuthenticationSchemes(schemes);
                policy.AddRequirements(new PermissionRequirement(area, action));
            });
        }
    });

    // Register the scoped authorization handler (must be scoped because it
    // depends on IEffectivePermissionService which uses a scoped DbContext).
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

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
    builder.Services.AddUserManagement();
    builder.Services.AddAdminPanelServices();
    builder.Services.AddEmailServices(builder.Configuration);
    builder.Services.AddSignalR();
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
            options.JsonSerializerOptions.Converters.Add(
                new UtcDateTimeJsonConverter());
            options.JsonSerializerOptions.Converters.Add(
                new UtcNullableDateTimeJsonConverter());
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
    // Ensure file-storage directory exists (B-P11 / DocumentService)
    // -----------------------------------------------------------------------
    var storagePath = Path.Combine(
        builder.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
        "uploads", "documents");
    Directory.CreateDirectory(storagePath);

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

    // Only redirect to HTTPS in non-Development environments.
    // In Development the dev certificate may not be trusted, causing VS to
    // report "Unable to connect to web server 'https'". Use the 'http' profile
    // for local development, or run: dotnet dev-certs https --trust
    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();
    app.UseCors("AllowAngularApp");

    app.UseAuthentication();
    app.UseAuthorization();
    app.UsePermissionEnforcement();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Relative path so Swagger UI works whether the app is hosted at the
        // site root (e.g. localhost:7135/swagger) or under an IIS sub-path
        // (e.g. apps.ku.ac.ae/actiontrackerAPIs/swagger).
        options.SwaggerEndpoint("v1/swagger.json", "Action Tracker API v1");
        options.RoutePrefix = "swagger";
    });

    // Redirect root to Swagger UI so opening the app shows the API docs.
    // Use LocalRedirect with "~/swagger" so it works when the app is hosted
    // under an IIS sub-path (e.g. https://apps.ku.ac.ae/actiontrackerAPIs/).
    app.MapGet("/", () => Results.LocalRedirect("~/swagger")).ExcludeFromDescription();

    app.MapControllers();
    app.MapHub<ActionTracker.Infrastructure.Hubs.NotificationHub>("/hubs/notifications");

    // -----------------------------------------------------------------------
    // Auto-migrate: create / update the database schema on startup
    // -----------------------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // -----------------------------------------------------------------------
    // Seed required roles (Admin, Manager, User, Viewer, …)
    await RoleSeeder.SeedAsync(app.Services);

    // -----------------------------------------------------------------------
    // Seed permission catalog (areas, actions, mappings) — must run first
    using (var scope = app.Services.CreateScope())
    {
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
                         .GetRequiredService<ILoggerFactory>()
                         .CreateLogger(nameof(PermissionCatalogSeeder));
        await PermissionCatalogSeeder.SeedAsync(db, logger);
    }

    // -----------------------------------------------------------------------
    // Seed default role permissions
    using (var scope = app.Services.CreateScope())
    {
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
                         .GetRequiredService<ILoggerFactory>()
                         .CreateLogger(nameof(DefaultRolePermissionsSeeder));
        await DefaultRolePermissionsSeeder.SeedAsync(db, logger);
    }

    // -----------------------------------------------------------------------
    // Seed email templates
    using (var scope = app.Services.CreateScope())
    {
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
                         .GetRequiredService<ILoggerFactory>()
                         .CreateLogger(nameof(EmailTemplateSeeder));
        await EmailTemplateSeeder.SeedAsync(db, logger);
    }

    // -----------------------------------------------------------------------
    // Seed bootstrap admin user (idempotent)
    await AdminBootstrapSeeder.SeedAsync(app.Services);

    // -----------------------------------------------------------------------
    // Open Swagger UI in the default browser on startup (Development only).
    // Works whether the app is launched via `dotnet run`, Visual Studio, or
    // VS Code — independent of launchSettings.json's launchBrowser flag.
    // -----------------------------------------------------------------------
    if (app.Environment.IsDevelopment())
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            try
            {
                var addresses = app.Services
                    .GetRequiredService<IServer>()
                    .Features
                    .Get<IServerAddressesFeature>()?
                    .Addresses;

                // Prefer http over https locally to avoid dev-cert trust prompts.
                var baseUrl = addresses?.FirstOrDefault(a => a.StartsWith("http://"))
                              ?? addresses?.FirstOrDefault()
                              ?? "http://localhost:5134";

                var swaggerUrl = $"{baseUrl.TrimEnd('/')}/swagger";

                Process.Start(new ProcessStartInfo
                {
                    FileName = swaggerUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to launch browser to Swagger UI");
            }
        });
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
