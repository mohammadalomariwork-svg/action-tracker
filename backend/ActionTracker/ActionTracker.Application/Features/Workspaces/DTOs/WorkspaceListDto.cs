using System;
using System.Collections.Generic;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Lightweight workspace representation used in list views.
/// </summary>
public class WorkspaceListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OrganizationUnit { get; set; } = string.Empty;
    public string AdminUserNames { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProjectCount { get; set; }
    public int MilestoneCount { get; set; }
    public int ActionItemCount { get; set; }
    public int OpenActionItemCount { get; set; }

    /// <summary>Structured admin details for avatar rendering in the list.</summary>
    public List<WorkspaceListAdminDto> Admins { get; set; } = new();
}

/// <summary>
/// Minimal admin info for workspace list view — name + department for avatars.
/// </summary>
public class WorkspaceListAdminDto
{
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}
