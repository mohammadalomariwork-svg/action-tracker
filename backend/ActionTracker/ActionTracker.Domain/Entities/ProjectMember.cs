using ActionTracker.Domain.Enums;

namespace ActionTracker.Domain.Entities;

/// <summary>
/// A person linked to one project (WO-2026-11 P-2): additional project manager,
/// viewer or team member. Unique on (ProjectId, UserId, Role); a person may hold
/// several roles and gets their union. Removing a person deletes the row (like
/// <see cref="WorkspaceMember"/> and <see cref="ProjectSponsor"/>), so access ends on
/// the next request.
/// </summary>
public class ProjectMember
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ProjectMemberRole Role { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public string? AddedByUserId { get; set; }

    // Navigation
    public Project Project { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
