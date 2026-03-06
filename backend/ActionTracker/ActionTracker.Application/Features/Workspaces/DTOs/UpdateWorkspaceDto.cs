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
    public int Id { get; set; }

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
}
