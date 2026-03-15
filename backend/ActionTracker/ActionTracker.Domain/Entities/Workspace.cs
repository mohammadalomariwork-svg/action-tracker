using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Domain.Entities;

public class Workspace
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string OrganizationUnit { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Foreign key to the OrgUnit this workspace belongs to (for access scoping).</summary>
    public Guid? OrgUnitId { get; set; }

    public ICollection<WorkspaceAdmin> Admins { get; set; } = new List<WorkspaceAdmin>();
}
