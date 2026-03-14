namespace ActionTracker.Application.Permissions.DTOs;

public class UserPermissionOverrideDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string OrgUnitScope { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
    public bool IsGranted { get; set; }
    public string? Reason { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
