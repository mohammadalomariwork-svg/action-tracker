using ActionTracker.Application.Permissions;
using ActionTracker.Application.Permissions.Domain;
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
    DbSet<Comment>              Comments              { get; }
    DbSet<Document>             Documents             { get; }
    DbSet<RefreshToken>        RefreshTokens       { get; }
    DbSet<ApplicationUser>     Users               { get; }
    DbSet<KuEmployeeInfo>      KuEmployeeInfo { get; }
    DbSet<OrgUnit>             OrgUnits       { get; }
    DbSet<Workspace>           Workspaces     { get; }
    DbSet<WorkspaceAdmin>      WorkspaceAdmins { get; }

    // ── Projects feature sets ─────────────────────────────────────────────────
    DbSet<Project>          Projects        { get; }
    DbSet<ProjectSponsor>   ProjectSponsors { get; }
    DbSet<Milestone>        Milestones      { get; }
    DbSet<ProjectRisk>      ProjectRisks    { get; }

    DbSet<StrategicObjective> StrategicObjectives { get; }
    DbSet<Kpi>                Kpis                { get; }
    DbSet<KpiTarget>          KpiTargets          { get; }

    // ── Permissions feature sets ──────────────────────────────────────────────
    DbSet<RolePermission>         RolePermissions         { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }
    DbSet<AppPermissionArea>      PermissionAreas         { get; }
    DbSet<AppPermissionAction>    PermissionActions       { get; }
    DbSet<AreaPermissionMapping>  AreaPermissionMappings  { get; }

    // ── Email feature sets ───────────────────────────────────────────────────
    DbSet<EmailTemplate>     EmailTemplates       { get; }
    DbSet<EmailLog>          EmailLogs            { get; }

    // ── Notification feature sets ────────────────────────────────────────────
    DbSet<AppNotification>   AppNotifications     { get; }

    // ── Workflow feature sets ────────────────────────────────────────────────
    DbSet<ActionItemWorkflowRequest> ActionItemWorkflowRequests { get; }
    DbSet<ProjectApprovalRequest> ProjectApprovalRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
