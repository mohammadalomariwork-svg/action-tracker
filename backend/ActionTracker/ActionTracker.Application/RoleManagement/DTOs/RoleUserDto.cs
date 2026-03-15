namespace ActionTracker.Application.RoleManagement.DTOs;

public class RoleUserDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserDisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? OrgUnitId { get; set; }
    public string? OrgUnitName { get; set; }
}
