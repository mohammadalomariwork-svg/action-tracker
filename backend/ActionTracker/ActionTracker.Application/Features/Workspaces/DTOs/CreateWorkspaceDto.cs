using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Payload for creating a new workspace.
/// </summary>
public class CreateWorkspaceDto
{
    /// <summary>
    /// Human-readable title of the workspace.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Name of the organisational unit this workspace belongs to.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>
    /// ID of the organisational unit (used for access scoping).
    /// </summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>
    /// Optional strategic plan to attach to the workspace at creation time.
    /// Pass <c>null</c> for workspaces with no strategy.
    /// </summary>
    public Guid? StrategicPlanId { get; set; }

    /// <summary>
    /// When <c>true</c> the workspace is private: only its admins and members
    /// can see it, and its data is excluded from everyone else's views and stats.
    /// </summary>
    public bool IsPrivate { get; set; }

    /// <summary>
    /// One or more admin users to assign to the workspace.
    /// At least one admin is required.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one admin user is required.")]
    public List<WorkspaceAdminDto> Admins { get; set; } = new();
}
