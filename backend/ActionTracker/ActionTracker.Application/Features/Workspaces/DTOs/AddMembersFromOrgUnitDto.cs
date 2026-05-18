using System.ComponentModel.DataAnnotations;

namespace ActionTracker.Application.Features.Workspaces.DTOs;

/// <summary>
/// Request to bulk-add every active user in an org unit as workspace members.
/// </summary>
public class AddMembersFromOrgUnitRequestDto
{
    [Required]
    public Guid OrgUnitId { get; set; }
}

/// <summary>
/// Result of a bulk org-unit member add. Carries the newly-added members so the
/// UI can append them without a full refetch, plus counters for the summary toast.
/// </summary>
public class AddMembersFromOrgUnitResultDto
{
    public List<WorkspaceMemberDto> Added { get; set; } = new();
    public int SkippedAlreadyMembers      { get; set; }
    public int TotalUsersInOrgUnit         { get; set; }
}
