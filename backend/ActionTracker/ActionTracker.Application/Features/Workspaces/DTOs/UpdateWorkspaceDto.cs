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
    /// The <c>Id</c> of the AspNetUsers record for the workspace admin.
    /// </summary>
    [Required]
    public string AdminUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the workspace admin (denormalised for fast reads).
    /// </summary>
    [Required]
    public string AdminUserName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the workspace is active. Set to <c>false</c> to deactivate.
    /// </summary>
    public bool IsActive { get; set; }
}
