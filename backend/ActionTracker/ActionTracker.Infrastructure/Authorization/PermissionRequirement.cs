using Microsoft.AspNetCore.Authorization;

namespace ActionTracker.Infrastructure.Authorization;

/// <summary>
/// Authorization requirement that demands a specific permission area + action
/// from the effective-permission system.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Human-readable area name, e.g. "Projects".</summary>
    public string Area { get; }

    /// <summary>Human-readable action name, e.g. "View".</summary>
    public string Action { get; }

    public PermissionRequirement(string area, string action)
    {
        Area   = area;
        Action = action;
    }
}
