using ActionTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

using PM = ActionTracker.Application.Features.Projects.Models;

namespace ActionTracker.Application.Common.Interfaces;

/// <summary>
/// Abstraction over AppDbContext used by Application services to avoid
/// a circular dependency with ActionTracker.Infrastructure.
/// </summary>
public interface IAppDbContext
{
    // ── Legacy / shared domain sets ───────────────────────────────────────────
    DbSet<ActionItem>          ActionItems    { get; }
    DbSet<RefreshToken>        RefreshTokens  { get; }
    DbSet<ApplicationUser>     Users          { get; }
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
    DbSet<PM.StrategicObjective>    WorkspaceStrategicObjectives { get; }
    DbSet<PM.Project>               Projects                     { get; }
    DbSet<PM.Milestone>             Milestones                   { get; }
    /// <summary>
    /// Project/milestone-scoped action items (int PK).
    /// Distinct from the legacy domain <c>ActionItem</c>.
    /// Stored in table "ProjectActionItems".
    /// </summary>
    DbSet<PM.ActionItem>            ProjectActionItems            { get; }
    DbSet<PM.Comment>               Comments                     { get; }
    DbSet<PM.ProjectDocument>       ProjectDocuments              { get; }
    DbSet<PM.ActionDocument>        ActionDocuments               { get; }
    DbSet<PM.ProjectBudget>         ProjectBudgets                { get; }
    DbSet<PM.Contract>              Contracts                    { get; }
    DbSet<PM.ProjectBaseline>       ProjectBaselines              { get; }
    DbSet<PM.BaselineChangeRequest> BaselineChangeRequests        { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
