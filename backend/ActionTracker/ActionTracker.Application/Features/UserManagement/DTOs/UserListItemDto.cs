namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class UserListItemDto
{
    public string      Id          { get; set; } = string.Empty;
    public string      UserName    { get; set; } = string.Empty;
    public string      Email       { get; set; } = string.Empty;
    public string      FullName    { get; set; } = string.Empty;
    public string?     PhoneNumber { get; set; }
    public bool        IsADUser    { get; set; }
    public bool        IsActive    { get; set; }
    public List<string> Roles      { get; set; } = [];
    public DateTime    CreatedAt   { get; set; }
    public Guid?       OrgUnitId   { get; set; }
    public string?     OrgUnitName { get; set; }
}
