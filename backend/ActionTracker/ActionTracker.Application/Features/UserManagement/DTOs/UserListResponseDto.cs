namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class UserListResponseDto
{
    public List<UserListItemDto> Users      { get; set; } = [];
    public int                   TotalCount { get; set; }
    public int                   Page       { get; set; }
    public int                   PageSize   { get; set; }
}
