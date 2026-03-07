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
    /// One or more admin users to assign to the workspace.
    /// At least one admin is required.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one admin user is required.")]
    public List<WorkspaceAdminDto> Admins { get; set; } = new();
}
