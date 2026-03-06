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
    /// The <c>Id</c> of the AspNetUsers record for the workspace admin.
    /// </summary>
    [Required]
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the workspace admin (denormalised for fast reads).
    /// </summary>
    [Required]
    public string AdminUserName { get; set; } = string.Empty;
}
