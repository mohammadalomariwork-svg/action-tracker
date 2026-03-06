namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class AssignUserOrgUnitRequestDto
{
    /// <summary>The org unit ID to assign to the user. Null to unassign.</summary>
    public Guid? OrgUnitId { get; set; }
}
