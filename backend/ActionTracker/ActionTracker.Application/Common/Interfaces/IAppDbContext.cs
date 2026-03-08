using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Abstraction over AppDbContext used by Application services to avoid
/// a circular dependency with ActionTracker.Infrastructure.
/// </summary>
public interface IAppDbContext
{
    // ── Legacy / shared domain sets ───────────────────────────────────────────
    DbSet<ActionItem>           ActionItems          { get; }
    DbSet<ActionItemAssignee>   ActionItemAssignees  { get; }
    DbSet<ActionItemEscalation> ActionItemEscalations { get; }
    DbSet<ActionItemComment>    ActionItemComments    { get; }
    DbSet<Document>             Documents             { get; }
    DbSet<RefreshToken>        RefreshTokens       { get; }
    DbSet<ApplicationUser>     Users               { get; }
    DbSet<KuEmployeeInfo>      KuEmployeeInfo { get; }
    DbSet<OrgUnit>             OrgUnits       { get; }
    DbSet<Workspace>           Workspaces     { get; }
    DbSet<WorkspaceAdmin>      WorkspaceAdmins { get; }

    // ── Projects feature sets ─────────────────────────────────────────────────
    /// <summary>
    /// Workspace-scoped strategic objectives (int PK).
    /// Distinct from the admin-panel <c>StrategicObjective</c> (Guid PK).
    /// Stored in table "WorkspaceStrategicObjectives".
    /// </summary>
   

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
