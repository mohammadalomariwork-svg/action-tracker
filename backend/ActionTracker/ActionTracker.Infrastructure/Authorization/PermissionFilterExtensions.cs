namespace ActionTracker.Infrastructure.Authorization;

/// <summary>
/// Extension methods for wiring permission enforcement into the ASP.NET Core pipeline.
/// </summary>
public static class PermissionFilterExtensions
{
    /// <summary>
    /// Placeholder for future permission-enforcement middleware.
    /// Call after <c>app.UseAuthorization()</c>.
    /// </summary>
    public static IApplicationBuilder UsePermissionEnforcement(this IApplicationBuilder app)
    {
        // Reserved for future middleware — e.g. global permission audit logging.
        return app;
    }
}
