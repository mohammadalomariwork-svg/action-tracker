using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>Payload for POST /api/workspaces/{id}/members.</summary>
public class AddWorkspaceMemberDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;
}
