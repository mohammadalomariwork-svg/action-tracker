namespace ActionTracker.Application.Permissions.DTOs;

public class PermissionMatrixDto
{
    public string RoleName { get; set; } = string.Empty;
    public List<RolePermissionDto> Permissions { get; set; } = new();
}
