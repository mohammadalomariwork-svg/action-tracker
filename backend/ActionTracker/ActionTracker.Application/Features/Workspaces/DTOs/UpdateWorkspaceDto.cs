using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Payload for updating an existing workspace.
/// </summary>
public class UpdateWorkspaceDto
{
    /// <summary>
    /// Primary key of the workspace to update.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Updated title of the workspace.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Updated organisational unit name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    /// <summary>
    /// ID of the organisational unit (used for access scoping).
    /// </summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>
    /// Optional strategic plan linked to the workspace. Pass <c>null</c> to clear.
    /// </summary>
    public Guid? StrategicPlanId { get; set; }

    /// <summary>
    /// Replacement admin user list. The existing admin list is fully replaced
    /// by this set. At least one admin is required.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one admin user is required.")]
    public List<WorkspaceAdminDto> Admins { get; set; } = new();

    /// <summary>
    /// Whether the workspace is active. Set to <c>false</c> to deactivate.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When <c>true</c> the workspace is private: only its admins and members
    /// can see it, and its data is excluded from everyone else's views and stats.
    /// <c>null</c> leaves the current setting unchanged.
    /// </summary>
    public bool? IsPrivate { get; set; }
}
