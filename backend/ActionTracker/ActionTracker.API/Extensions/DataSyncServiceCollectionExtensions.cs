using ActionTracker.Application.Features.DataSync;
using ActionTracker.Infrastructure.DataSync;
using Hangfire;
using Hangfire.SqlServer;

namespace ActionTracker.API.Extensions;

/// <summary>
/// Hangfire + Data Sync registration (Admin Panel → Data Sync).
/// Hangfire persists its queue in the application database under the
/// <c>HangFire</c> schema; tables are created automatically on first start.
/// </summary>
public static class DataSyncServiceCollectionExtensions
{
    /// <summary>Environment name used by the integration-test host.</summary>
    public const string TestingEnvironment = "Testing";

    public static IServiceCollection AddDataSyncServices(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // ── Options ──────────────────────────────────────────────────────────
        services.Configure<EmployeeInfoSyncOptions>(configuration.GetSection(EmployeeInfoSyncOptions.SectionName));

        // ── Data-sync services ───────────────────────────────────────────────
        services.AddScoped<IDataSyncJobHandler, EmployeeInfoSyncJobHandler>();
        services.AddScoped<IDataSyncJobRunner,  DataSyncJobRunner>();
        services.AddScoped<IDataSyncService,    DataSyncService>();
        services.AddScoped<DataSyncHangfireJob>();

        // The integration-test host swaps EF for InMemory; Hangfire must not
        // open its own SQL connection there, so use the no-op scheduler.
        if (environment.IsEnvironment(TestingEnvironment))
        {
            services.AddSingleton<IDataSyncScheduler, NoOpDataSyncScheduler>();
            return services;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        // ── Hangfire ─────────────────────────────────────────────────────────
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                SchemaName                   = "HangFire",
                PrepareSchemaIfNecessary     = true,
                QueuePollInterval            = TimeSpan.FromSeconds(15),
                SlidingInvisibilityTimeout   = TimeSpan.FromMinutes(5),
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks           = true,
            }));

        // Small worker pool: sync jobs are few and serialised by DisableConcurrentExecution.
        services.AddHangfireServer(options =>
        {
            options.ServerName  = $"{Environment.MachineName}:data-sync";
            options.WorkerCount = 2;
            options.Queues      = new[] { "default" };
        });

        services.AddSingleton<IDataSyncScheduler, HangfireDataSyncScheduler>();
        return services;
    }
}
