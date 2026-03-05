namespace ActionTracker.Application.Features.UserManagement.DTOs;

public class EmployeeSearchRequestDto
{
    public string SearchTerm { get; set; } = string.Empty;
    public int    Page       { get; set; } = 1;
    public int    PageSize   { get; set; } = 10;
}
