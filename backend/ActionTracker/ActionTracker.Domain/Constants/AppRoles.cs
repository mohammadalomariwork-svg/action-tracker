namespace ActionTracker.Domain.Constants;

/// <summary>
/// Single source of truth for all application role names.
/// Use these constants everywhere roles are referenced to avoid magic strings.
/// </summary>
public static class AppRoles
{
    // ── System-level roles ───────────────────────────────────────────────────
    /// <summary>Full system access, admin panel, all management capabilities.</summary>
    public const string Admin = "Admin";

    /// <summary>Workspace-scoped admin: creates workspaces and manages user access within their org unit.</summary>
    public const string WorkspaceAdmin = "Workspace Admin";

    // ── PMO roles ────────────────────────────────────────────────────────────
    /// <summary>Cross-portfolio visibility, approve charters, set standards, strategic reporting.</summary>
    public const string PmoHead = "PMO Head";

    /// <summary>Portfolio reporting, data entry support, no approval authority.</summary>
    public const string PmoAnalyst = "PMO Analyst";

    // ── Project roles ────────────────────────────────────────────────────────
    /// <summary>Approves charter and closure, monitors project health, high-level view.</summary>
    public const string ProjectSponsor = "Project Sponsor";

    /// <summary>Full project management: create/edit all project artifacts, manage team, submit reports.</summary>
    public const string ProjectManager = "Project Manager";

    /// <summary>Supports PM: data entry, scheduling support, document management, no approval.</summary>
    public const string ProjectCoordinator = "Project Coordinator";

    /// <summary>Assigned action items, log progress, raise issues, view project data.</summary>
    public const string TeamMember = "Team Member";

    // ── Legacy roles (kept for backward compatibility) ───────────────────────
    /// <summary>Legacy generic manager role — preserved as originally configured in the database.</summary>
    public const string Manager = "Manager";

    /// <summary>Legacy generic user role — preserved as originally configured in the database.</summary>
    public const string User = "User";

    /// <summary>Legacy read-only role — preserved as originally configured in the database.</summary>
    public const string Viewer = "Viewer";
}
