using System;
using System.Collections.Generic;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Full representation of a workspace returned from the API.
/// </summary>
public class WorkspaceResponseDto
{
    /// <summary>Primary key of the workspace.</summary>
    public Guid Id { get; set; }

    /// <summary>Human-readable title of the workspace.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Name of the organisational unit this workspace belongs to.</summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>ID of the organisational unit (for access scoping).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>Optional strategic plan linked to this workspace.</summary>
    public Guid? StrategicPlanId { get; set; }

    /// <summary>Display name of the linked strategic plan (when one is set).</summary>
    public string? StrategicPlanName { get; set; }

    /// <summary>All admin users assigned to this workspace.</summary>
    public List<WorkspaceAdminDto> Admins { get; set; } = new();

    /// <summary>Regular members assigned to this workspace.</summary>
    public List<WorkspaceMemberDto> Members { get; set; } = new();

    /// <summary>Users eligible to be selected as Project Manager in this workspace.</summary>
    public List<WorkspaceProjectManagerDto> ProjectManagers { get; set; } = new();

    /// <summary>Ordered approval chain for strategic projects in this workspace.</summary>
    public List<WorkspaceStrategicProjectApproverDto> StrategicProjectApprovers { get; set; } = new();

    /// <summary>Whether the workspace is currently active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Private workspaces are visible only to their admins and members.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>UTC timestamp when the workspace was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// WO-2026-11 P-2b: the caller may change this workspace's settings (the workspace,
    /// admins, members, Project Managers list, approvers, classifications, calendar,
    /// strategy link): an admin of it or the Admin role. The Workspaces.Edit /
    /// Workspaces.Delete permission is needed on top.
    /// </summary>
    public bool CanManageSettings { get; set; }

    /// <summary>
    /// WO-2026-11 P-2b (B2): the caller may create a project here (an admin or member of the
    /// workspace, on its Project Managers list, or the Admin / PMO Head / PMO Analyst role).
    /// <c>Projects.Create</c> is needed on top.
    /// </summary>
    public bool CanCreateProjects { get; set; }

    /// <summary>
    /// WO-2026-11 P-2b (B2): the caller may add a standalone action item here (an admin or
    /// member, or the Admin / PMO roles). <c>ActionItems.Create</c> is needed on top.
    /// </summary>
    public bool CanCreateActionItems { get; set; }
}
