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

    /// <summary>All admin users assigned to this workspace.</summary>
    public List<WorkspaceAdminDto> Admins { get; set; } = new();

    /// <summary>Whether the workspace is currently active.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC timestamp when the workspace was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update, or <c>null</c> if never updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
