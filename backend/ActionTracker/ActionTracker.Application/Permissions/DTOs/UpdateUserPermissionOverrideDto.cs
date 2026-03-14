namespace ActionTracker.Application.Permissions.DTOs;

public class UpdateUserPermissionOverrideDto
{
    public int OrgUnitScope { get; set; }
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsGranted { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}
