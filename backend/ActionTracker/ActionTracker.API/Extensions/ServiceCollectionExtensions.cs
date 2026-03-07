using System.Text;
using ActionTracker.Application.Common.Interfaces;
using ActionTracker.Application.Features.ActionItems.Interfaces;
using ActionTracker.Application.Features.ActionItems.Services;
using ActionTracker.Application.Features.Auth.Interfaces;
using ActionTracker.Application.Features.Auth.Services;
using ActionTracker.Application.Features.Dashboard.Interfaces;
using ActionTracker.Application.Features.Dashboard.Services;
using ActionTracker.Application.Features.Reports.Interfaces;
using ActionTracker.Application.Features.Reports.Services;
using ActionTracker.Application.Helpers;
using ActionTracker.Domain.Entities;
using ActionTracker.Application.Features.Kpis.Interfaces;
using ActionTracker.Application.Features.OrgChart.Interfaces;
using ActionTracker.Application.Features.StrategicObjectives.Interfaces;
using ActionTracker.Application.Features.UserManagement.Interfaces;
using ActionTracker.Application.Features.Workspaces.Interfaces;
using ActionTracker.Application.Features.Workspaces.Services;
using ActionTracker.Infrastructure.Data;
using ActionTracker.Infrastructure.Helpers;
using ActionTracker.Infrastructure.Services;
using ActionTracker.API.Middleware;

// Projects feature — aliased to avoid ambiguity with the admin-panel
// IStrategicObjectiveService (Guid PK) registered in AddAdminPanelServices.
using IProjectStrategicObjectiveService = ActionTracker.Application.Features.Projects.Interfaces.IStrategicObjectiveService;
using ProjectStrategicObjectiveService  = ActionTracker.Application.Features.Projects.Services.StrategicObjectiveService;
using IProjectService          = ActionTracker.Application.Features.Projects.Interfaces.IProjectService;
using ProjectService           = ActionTracker.Application.Features.Projects.Services.ProjectService;
using IMilestoneService        = ActionTracker.Application.Features.Projects.Interfaces.IMilestoneService;
using MilestoneService         = ActionTracker.Application.Features.Projects.Services.MilestoneService;
using IProjectActionItemService = ActionTracker.Application.Features.Projects.Interfaces.IActionItemService;
using ProjectActionItemService  = ActionTracker.Application.Features.Projects.Services.ActionItemService;
using ICommentService          = ActionTracker.Application.Features.Projects.Interfaces.ICommentService;
using CommentService           = ActionTracker.Application.Features.Projects.Services.CommentService;
using IDocumentService         = ActionTracker.Application.Features.Projects.Interfaces.IDocumentService;
using DocumentService          = ActionTracker.Application.Features.Projects.Services.DocumentService;
using IBudgetService           = ActionTracker.Application.Features.Projects.Interfaces.IBudgetService;
using BudgetService            = ActionTracker.Application.Features.Projects.Services.BudgetService;
using IBaselineService         = ActionTracker.Application.Features.Projects.Interfaces.IBaselineService;
using BaselineService          = ActionTracker.Application.Features.Projects.Services.BaselineService;

// Aliases to distinguish the two IAuthService definitions that currently coexist:
// the original (Interfaces.IAuthService) and the new contract (Features.Auth.IAuthService).
using INewAuthService = ActionTracker.Application.Features.Auth.IAuthService;
using NewAuthService  = ActionTracker.Infrastructure.Features.Auth.AuthService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ActionTracker.API.Extensions;

public static class ServiceCollectionExtensions
{
    // -------------------------------------------------------------------------
    // 1. JWT Authentication
    // -------------------------------------------------------------------------

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration config)
    {
        var secretKey = config["JwtSettings:SecretKey"]!;
        var issuer    = config["JwtSettings:Issuer"];
        var audience  = config["JwtSettings:Audience"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(
                                                  Encoding.UTF8.GetBytes(secretKey)),
                    ValidateIssuer   = true,
                    ValidIssuer      = issuer,
                    ValidateAudience = true,
                    ValidAudience    = audience,
                    ValidateLifetime = true,
                    ClockSkew        = TimeSpan.Zero,
                };
            });

        return services;
    }

    // -------------------------------------------------------------------------
    // 2. Application services (all Scoped)
    // -------------------------------------------------------------------------

    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        // Map IAppDbContext → AppDbContext (registered by AddDbContext)
        services.AddScoped<IAppDbContext>(sp =>
            sp.GetRequiredService<AppDbContext>());

        // Feature services — legacy contract (Interfaces.IAuthService)
        services.AddScoped<IAuthService,       AuthService>();
        services.AddScoped<IActionItemService, ActionItemService>();
        services.AddScoped<IDashboardService,  DashboardService>();
        services.AddScoped<IReportService,     ReportService>();

        // New auth contract (Application.Features.Auth.IAuthService)
        services.AddScoped<INewAuthService, NewAuthService>();

        // Helpers
        services.AddScoped<JwtHelper>();
        services.AddScoped<JwtTokenHelper>();
        services.AddScoped<CsvExportHelper>();

        // Middleware (IMiddleware implementations require explicit DI registration)
        services.AddTransient<ExceptionMiddleware>();
        services.AddTransient<RequestLoggingMiddleware>();

        return services;
    }

    // -------------------------------------------------------------------------
    // 3. Swagger with Bearer JWT security
    // -------------------------------------------------------------------------

    public static IServiceCollection AddSwaggerWithJwt(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title   = "Action Tracker API",
                Version = "v1",
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name         = "Authorization",
                Description  = "Enter: Bearer {token}",
                Type         = SecuritySchemeType.Http,
                Scheme       = "bearer",
                BearerFormat = "JWT",
                In           = ParameterLocation.Header,
                Reference    = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer",
                },
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() },
            });
        });

        return services;
    }

    // -------------------------------------------------------------------------
    // 4. CORS policy
    // -------------------------------------------------------------------------

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration config)
    {
        var allowedOrigins = config
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
            options.AddPolicy("ActionTrackerCors", policy =>
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()));

        return services;
    }

    // -------------------------------------------------------------------------
    // 6. User Management feature
    // -------------------------------------------------------------------------

    public static IServiceCollection AddUserManagement(this IServiceCollection services)
    {
        services.AddScoped<IUserManagementService, UserManagementService>();
        return services;
    }

    // -------------------------------------------------------------------------
    // 5. ASP.NET Core Identity
    // -------------------------------------------------------------------------

    public static IServiceCollection AddIdentityConfiguration(
        this IServiceCollection services, IConfiguration config)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength        = 8;
                options.Password.RequireDigit          = true;
                options.Password.RequireUppercase      = true;
                options.Password.RequireLowercase      = false;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        return services;
    }

    // -------------------------------------------------------------------------
    // 7. Admin panel services (OrgUnit, StrategicObjective, Kpi)
    // -------------------------------------------------------------------------

    public static IServiceCollection AddAdminPanelServices(this IServiceCollection services)
    {
        services.AddScoped<IUserLookupService,         UserLookupService>();
        services.AddScoped<IOrgUnitService,            OrgUnitService>();
        services.AddScoped<IStrategicObjectiveService, StrategicObjectiveService>();
        services.AddScoped<IKpiService,                KpiService>();
        services.AddScoped<IWorkspaceService,          WorkspaceService>();
        return services;
    }

    // -------------------------------------------------------------------------
    // 8. Projects feature services (B-P11)
    //    Grouped separately to avoid confusion with the admin-panel
    //    IStrategicObjectiveService (Guid PK) registered above.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers all Projects-feature application services (B-P07 – B-P09).
    /// Type aliases at the top of this file resolve the name collision between
    /// the admin-panel <c>IStrategicObjectiveService</c> (Guid PK) and the
    /// workspace-scoped one (int PK) used by the Projects feature.
    /// </summary>
    public static IServiceCollection AddProjectsFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectStrategicObjectiveService, ProjectStrategicObjectiveService>();
        services.AddScoped<IProjectService,             ProjectService>();
        services.AddScoped<IMilestoneService,           MilestoneService>();
        services.AddScoped<IProjectActionItemService,   ProjectActionItemService>();
        services.AddScoped<ICommentService,             CommentService>();
        services.AddScoped<IDocumentService,            DocumentService>();
        services.AddScoped<IBudgetService,              BudgetService>();
        services.AddScoped<IBaselineService,            BaselineService>();
        return services;
    }
}
